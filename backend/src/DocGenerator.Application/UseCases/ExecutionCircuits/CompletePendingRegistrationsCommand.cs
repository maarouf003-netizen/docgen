using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// إعادة قيد ملفات معلّقة بأرقام جديدة — عملية **ذرّية جماعية**: تتحقق من الملكية والعلَم
/// داخل المعاملة، وتطبّع الرقم/النوع/السنة، وتفرض الوحدانية عبر الكاتب الوحيد (قفل + طبقتا
/// تكرار)، وتزامن السجل، وتعيد بناء البحث/البيانات الكاملة، وتكتب وقعة إعادة قيد وتدقيقًا
/// حقيليًا لكل ملف ثم تدقيقًا جماعيًا، وتنظّف تنبيهات المعلقات. سباق القيد الفريد → `409`.
/// </summary>
public sealed record CompletePendingRegistrationsCommand(
    int LawyerId,
    string? ActorName,
    CompleteRegistrationsRequest Request) : ICommand<int>, ITransactionalRequest;

public sealed class CompletePendingRegistrationsCommandHandler
    : CommandHandlerBase<CompletePendingRegistrationsCommand, int>
{
    private readonly IDocumentRepository _documents;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IUserRepository _users;
    private readonly IHeadAlertRepository _alerts;
    private readonly IDocumentNumberRegistry? _numberRegistry;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IReferenceDataCache? _cache;

    public CompletePendingRegistrationsCommandHandler(
        IDocumentRepository documents,
        IRepository<DocumentOccurrence> occurrences,
        IUserRepository users,
        IHeadAlertRepository alerts,
        IUnitOfWork uow,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<CompletePendingRegistrationsCommand, int>> behaviors,
        IDocumentNumberRegistry? numberRegistry = null,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _documents = documents;
        _occurrences = occurrences;
        _users = users;
        _alerts = alerts;
        _uow = uow;
        _audit = audit;
        _dbErrors = dbErrors;
        _clock = clock;
        _timeZone = timeZone;
        _numberRegistry = numberRegistry;
        _cache = cache;
    }

    protected override async Task<int> HandleCoreAsync(
        CompletePendingRegistrationsCommand command, CancellationToken ct)
    {
        var request = command.Request;
        if (request.Entries is null || request.Entries.Count == 0)
            throw new ArgumentException("لا توجد ملفات لإعادة القيد");
        var ids = request.Entries.Select(e => e.DocumentId).Distinct().ToList();
        if (ids.Count != request.Entries.Count)
            throw new ArgumentException("تكرار في الملفات المرسلة");

        var saved = 0;
        try
        {
            var docs = await _documents.ListByIdsForUpdateAsync(ids, ct);
            if (docs.Count != ids.Count)
                throw new ArgumentException("أحد الملفات غير موجود");
            var byId = docs.ToDictionary(d => d.Id);
            // فحص الملكية + flag داخل المعاملة.
            foreach (var e in request.Entries)
            {
                var doc = byId[e.DocumentId];
                if (doc.IsDeleted)
                    throw new ArgumentException($"الملف ({e.DocumentId}) محذوف");
                if (doc.CreatedById != command.LawyerId)
                    throw new ArgumentException($"الملف ({e.DocumentId}) ليس من ملفاتك");
                if (!doc.NeedsRegistration)
                    throw new ArgumentException($"الملف ({e.DocumentId}) ليس بانتظار إعادة القيد");
            }
            // تطبيع رقمي إلزامي + تحقق وحدانية ذري لكل ملف.
            var now = DateTime.UtcNow;
            var completedAudit = new List<(Document Doc, Dictionary<string, string?> Before, string Number, string Type, string Year)>();
            foreach (var e in request.Entries)
            {
                var doc = byId[e.DocumentId];
                var number = DigitNormalizer.NormalizeDigits(e.FileNumber);
                var year = DigitNormalizer.NormalizeDigits(e.FileYear);
                var type = (e.FileType ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(number))
                    throw new ArgumentException($"الرقم الجديد إلزامي للملف ({e.DocumentId})");
                if (string.IsNullOrWhiteSpace(type))
                    throw new ArgumentException($"النوع إلزامي للملف ({e.DocumentId})");
                if (string.IsNullOrWhiteSpace(year))
                    throw new ArgumentException($"السنة إلزامية للملف ({e.DocumentId})");
                DocumentValidator.ValidateFileYearNotFuture(year, ServerClock.CurrentYear(_clock, _timeZone));
                if (doc.ExecutionCircuitId is null)
                    throw new ArgumentException($"الملف ({e.DocumentId}) بلا دائرة");
                // الوحدانية عبر الكاتب الوحيد (قفل + طبقتا تكرار) عند توفره؛
                // وإلا الفحص الخدمي القديم للاختبارات المبنية يدويًا.
                if (_numberRegistry is null
                    && await _documents.ExistsActiveWithNumberByCircuitAsync(
                        doc.Id, doc.ExecutionCircuitId, doc.CourtNorm, number, type, year, ct))
                    throw new DocumentConflictException($"رقم الأساس {number} مكرر في دائرة الملف ({e.DocumentId}) لسنة {year}");

                // لقطة «قبل» للتدقيق الحقلي (S5) — قبل أي طفرة (بعد كل الحراس).
                var before = DocumentChangeTracker.Capture(doc);
                doc.FileNumber = number;
                doc.FileType = type;
                doc.FileYear = year;
                doc.NeedsRegistration = false;
                doc.IsDraft = false;
                doc.UpdatedAt = now;
                doc.Version++;
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
                _documents.Update(doc);

                if (_numberRegistry is not null)
                    await _numberRegistry.SyncRegistrationAsync(
                        doc, command.LawyerId, command.ActorName, request.WarningToken, ct);

                var occ = new DocumentOccurrence
                {
                    DocumentId = doc.Id,
                    OccurrenceType = OccurrenceTypeCatalog.CircuitReregistered,
                    Source = OccurrenceSourceCatalog.System,
                    EventDate = now,
                    FileNumber = number,
                    FileType = doc.FileType,
                    Year = int.TryParse(year, out var yy) ? yy : null,
                    FromCircuitName = doc.Court,
                    ToCircuitName = doc.Court,
                    CreatedById = command.LawyerId,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                await _occurrences.AddAsync(occ, ct);
                completedAudit.Add((doc, before, number, type, year));
                saved++;
            }
            await _uow.SaveChangesAsync(ct);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);
            // تدقيق حقلي لكل ملف (S5) — داخل المعاملة بعد الحفظ الرئيسي.
            foreach (var (doc, before, number, type, year) in completedAudit)
                await ExecutionCircuitRules.LogDocumentChangeAsync(_audit, doc, command.ActorName, "complete_registrations",
                    $"أعاد قيد الملف ({doc.Id}) برقم {number}/{type}/{year}", before, ct);
            await _audit.LogAsync(command.ActorName, "complete_registrations", null, null,
                $"أعاد قيد {saved} ملفًا بأرقام جديدة", ct);

            // خطاف التنظيف DeleteByPendingRegistration: يُستدعى عند اكتمال إعادة قيد آخر ملف معلق للمحامي.
            await ExecutionCircuitRules.CleanupPendingAlertsAsync(
                command.LawyerId, _documents, _users, _alerts, ct);
            await _uow.SaveChangesAsync(ct);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException("رقم الأساس مكرر — أُدخل أثناء الحفظ من مستخدم آخر، أعد المحاولة برقم مختلف", ex);
        }
        return saved;
    }
}
