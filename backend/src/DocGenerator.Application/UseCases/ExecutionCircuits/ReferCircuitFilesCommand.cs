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
/// إحالة ملفات من دائرة مصدر إلى دائرة هدف ومالك جديد (إفراغ الدائرة المصدر) —
/// يملك مفتاح عدم التكرار (`RF-011`) ومعاملة النواة وتدقيق `refer_circuit_files`
/// والوقوعات والتنبيه المدمج عبر `Outbox`.
/// <para>
/// **حدود المعاملة:** يستثني وسم `ITransactionalRequest` عمدًا: حجز المفتاح وإتمام
/// اللقطة يحيطان بمعاملة النواة (والتحرير عند الفشل)، وتحقّقات الفرع/المدخلات تسبق
/// فتح المعاملة كما الأصل — فلا معاملة متداخلة ولا لقطة تُكتب قبل الالتزام.
/// </para>
/// </summary>
public sealed record ReferCircuitFilesCommand(
    int SourceCircuitId,
    int BranchId,
    int? OwnerSectionId,
    int ActorUserId,
    string? ActorName,
    ReferCircuitFilesRequest Request,
    string? IdempotencyKey = null) : ICommand<ReferCircuitFilesResult>;

public sealed class ReferCircuitFilesCommandHandler
    : CommandHandlerBase<ReferCircuitFilesCommand, ReferCircuitFilesResult>
{
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<Branch> _branches;
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IUserRepository _users;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IIdempotencyStore? _idempotency;
    private readonly IReferenceDataCache? _cache;

    public ReferCircuitFilesCommandHandler(
        IRepository<ExecutionCircuit> circuits,
        IRepository<Branch> branches,
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IRepository<DocumentOccurrence> occurrences,
        IUserRepository users,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<ReferCircuitFilesCommand, ReferCircuitFilesResult>> behaviors,
        IIdempotencyStore? idempotency = null,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _circuits = circuits;
        _branches = branches;
        _documents = documents;
        _delegations = delegations;
        _occurrences = occurrences;
        _users = users;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
        _idempotency = idempotency;
        _cache = cache;
    }

    protected override async Task<ReferCircuitFilesResult> HandleCoreAsync(
        ReferCircuitFilesCommand command, CancellationToken ct)
    {
        // RF-011: حجز المفتاح قبل أي تحقق يستحق 400 بلا حجز — التحققات الثقيلة داخل
        // النواة؛ التكرار بنفس البصمة يُعيد اللقطة المخزنة نفسها. البصمة بمعرفات
        // مرتبة حتى لا تُبطل إعادة ترتيب المصفوفة التكرار.
        var orderedIds = (command.Request.FileIds ?? new List<int>()).Distinct().OrderBy(id => id).ToList();
        var ticket = await IdempotencyGuard.BeginAsync(_idempotency, "circuits.refer-files",
            command.ActorUserId, command.IdempotencyKey,
            IdempotencyGuard.Fingerprint(new
            {
                sourceCircuitId = command.SourceCircuitId,
                fileIds = orderedIds,
                command.Request.TargetLawyerId,
                command.Request.TargetCircuitId,
                branchId = command.BranchId,
                ownerSectionId = command.OwnerSectionId,
            }), ct);

        ReferCircuitFilesResult result;
        try
        {
            result = await ReferFilesCoreAsync(command, ct);
        }
        catch
        {
            await IdempotencyGuard.ReleaseAsync(_idempotency, ticket, ct);
            throw;
        }

        // اللقطة بشكل جسم الاستجابة نفسه (مع البانر) ليُعاد حرفيًا عند التكرار.
        await IdempotencyGuard.CompleteAsync(_idempotency, ticket,
            IdempotencyGuard.Snapshot(new
            {
                result.ReferredCount,
                result.SkippedCount,
                result.RemainingCount,
                reminder = ExecutionCircuitService.EmptyingReminderBanner,
            }), ct: ct);
        return result;
    }

    /// <summary>نواة الإحالة (تحققات + نقل + تدقيق) — تُستدعى بعد حجز مفتاح عدم التكرار.</summary>
    private async Task<ReferCircuitFilesResult> ReferFilesCoreAsync(
        ReferCircuitFilesCommand command, CancellationToken ct)
    {
        ExecutionCircuitRules.RequireBranchWithGovernorate(
            await _branches.GetByIdAsync(command.BranchId, ct));
        if (command.Request.FileIds is null || command.Request.FileIds.Count == 0)
            throw new ArgumentException("حدد ملفًا واحدًا على الأقل للإحالة");
        if (command.SourceCircuitId == command.Request.TargetCircuitId)
            throw new ArgumentException("دائرة المصدر والهدف متماثلتان");

        var distinctIds = command.Request.FileIds.Distinct().ToList();
        int referred = 0, skipped = 0, remaining = 0;

        await _tx.RunAsync(async token =>
        {
            var source = await ExecutionCircuitRules.GetOwnedAsync(
                command.SourceCircuitId, command.BranchId, command.OwnerSectionId, _circuits, token);
            var target = await ExecutionCircuitRules.GetOwnedAsync(
                command.Request.TargetCircuitId, command.BranchId, command.OwnerSectionId, _circuits, token);
            if (!target.IsActive)
                throw new ArgumentException("دائرة الهدف معطلة — اختر دائرة نشطة");
            var sourceName = source.Name;
            var targetName = target.Name;

            // بدء الإفراغ يعطّل الدائرة الملغاة تلقائيًا (لا ملفات جديدة أثناء التفريغ).
            if (source.IsActive)
            {
                source.IsActive = false;
                source.UpdatedAt = DateTime.UtcNow;
                source.Version++;
                _circuits.Update(source);
            }

            var lawyer = await _users.GetByIdAsync(command.Request.TargetLawyerId, token);
            if (lawyer is null || lawyer.Role != UserRole.Lawyer || !lawyer.IsActive)
                throw new ArgumentException("المحامي الهدف غير موجود أو معطل");
            if (lawyer.BranchId != command.BranchId)
                throw new ArgumentException("المحامي الهدف ليس ضمن فرع الدائرة");
            var targetFullName = string.IsNullOrWhiteSpace(lawyer.FullName) ? lawyer.Username : lawyer.FullName;

            var docs = await _documents.ListByIdsForUpdateAsync(distinctIds, token);
            var byId = docs.ToDictionary(d => d.Id);
            foreach (var id in distinctIds)
            {
                if (!byId.TryGetValue(id, out var doc) || doc.IsDeleted)
                    throw new ArgumentException($"الملف ({id}) غير موجود في دائرة المصدر");
                if (doc.ExecutionCircuitId != command.SourceCircuitId)
                    throw new ArgumentException($"الملف ({id}) ليس من دائرة المصدر");
            }
            var now = DateTime.UtcNow;
            var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
            var referredAudit = new List<(Document Doc, Dictionary<string, string?> Before)>();
            foreach (var doc in docs)
            {
                // تخطي المسند لمالكه الحالي بصمت مع عدّاد متخطىً.
                if (doc.CreatedById == command.Request.TargetLawyerId && doc.ExecutionCircuitId == command.Request.TargetCircuitId)
                {
                    skipped++;
                    continue;
                }
                var oldNumber = EffectiveFileIdentity.Number(doc, currentYear) ?? doc.FileNumber;
                var oldType = doc.FileType;
                var oldYear = EffectiveFileIdentity.Year(doc, currentYear) ?? doc.FileYear;

                // لقطة «قبل» للتدقيق الحقلي (S5) — قبل أي طفرة.
                var before = DocumentChangeTracker.Capture(doc);
                doc.CreatedById = command.Request.TargetLawyerId;
                doc.Lawyer = targetFullName;
                doc.ExecutionCircuitId = command.Request.TargetCircuitId;
                doc.Court = target.Name;
                doc.CourtNorm = ArabicNameNormalizer.Normalize(target.Name);
                doc.FileNumber = null;
                doc.FileType = null;
                doc.FileYear = null;
                doc.NeedsRegistration = true;
                doc.IsDraft = false;
                doc.UpdatedAt = now;
                doc.Version++;
                // V-07: صفوف الدائرة القديمة تبقى غير ملغاة (توثيق وحجز في دائرتها/سنتها)،
                // ولا تظهر في هوية الدائرة الجديدة. لا حذف ولا إلغاء هنا.
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
                _documents.Update(doc);

                // وقعة الإحالة تُبنى بقاموس Details متغيرًا (يدخل الذيل عند نقل إنابة المناب)
                // وتُضاف في النهاية بتمريرة تسلسل واحدة — القاعدتان 2/3 (C2) تليانيان بلا استبعاد.
                var occDetails = new Dictionary<string, string>
                {
                    ["fromCircuit"] = sourceName,
                    ["toCircuit"] = targetName,
                    ["oldNumber"] = oldNumber ?? string.Empty,
                    ["oldType"] = oldType ?? string.Empty,
                    ["oldYear"] = oldYear ?? string.Empty,
                };
                int? oldYearNum = int.TryParse(oldYear, out var oy) ? oy : null;

                // الإنابات تتبع ملفاتها: المنيب المنقول يسحب إناباته كما هي؛ المناب المنقول يُحدَّث DelegatedCourt لوجهته.
                var outgoing = await _delegations.ListBySourceAsync(doc.Id, token);
                foreach (var g in outgoing)
                {
                    g.UpdatedAt = now;
                    _delegations.Update(g);
                }
                // قاعدة 2 (C2): المنيب المنقول يبلّغ مناباته الجاهزة — وقعة source-file-moved
                // على كل مناب مرتبط بإنابة صادرة، بأرقام المنيب القديمة. إنابة بلا مناب بعد → تُتخطى.
                if (outgoing.Count > 0)
                {
                    var mirrorMap = await _documents.MapMirrorsBySourceDelegationIdsAsync(
                        outgoing.Select(g => g.Id).ToList(), token);
                    foreach (var g in outgoing)
                    {
                        if (!mirrorMap.TryGetValue(g.Id, out var mirrorId)) continue;
                        await _occurrences.AddAsync(new DocumentOccurrence
                        {
                            DocumentId = mirrorId,
                            OccurrenceType = OccurrenceTypeCatalog.SourceFileMoved,
                            Source = OccurrenceSourceCatalog.System,
                            EventDate = now,
                            FileNumber = oldNumber,
                            FileType = oldType,
                            Year = oldYearNum,
                            FromCircuitName = sourceName,
                            ToCircuitName = targetName,
                            Details = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                            {
                                ["fromCircuit"] = sourceName,
                                ["toCircuit"] = targetName,
                            }),
                            CreatedById = command.ActorUserId,
                            CreatedAt = now,
                            UpdatedAt = now,
                        }, token);
                    }
                }
                // قاعدة 3 (C2): إن كان الملف المنقول منابًا (له SourceDelegationId) وإنابته
                // داخلية تستهدف دائرة المصدر، تُحدَّث للوجهة — وذيل تغيّر الوجهة يُدوَّن على
                // وقعة إحالة المناب، ووقعة متناظرة mirror-file-moved تُكتب على المنيب
                // (بأرقام المناب القديمة — لا تحميل للمنيب: FK فقط، وحارس حذف الملفات
                // يمنع حذف أي ملف له إنابة فبقاؤه مضمون).
                if (doc.SourceDelegationId is not null)
                {
                    var src = await _delegations.GetByIdAsync(doc.SourceDelegationId.Value, token);
                    if (src is not null && !src.IsExternal && src.DelegatedCircuitId == command.SourceCircuitId)
                    {
                        // oldCourt يُلتقط قبل الطفرات مع سقوط احتياطي إلى اسم دائرة المصدر عند الشذوذ.
                        var oldCourt = string.IsNullOrWhiteSpace(src.DelegatedCourt) ? sourceName : src.DelegatedCourt;
                        src.DelegatedCircuitId = command.Request.TargetCircuitId;
                        src.DelegatedCourt = target.Name;
                        src.DelegatedCourtNorm = ArabicNameNormalizer.Normalize(target.Name);
                        src.UpdatedAt = now;
                        _delegations.Update(src);
                        occDetails["delegationCircuitChange"] = $"من «{oldCourt}» إلى «{targetName}»";
                        await _occurrences.AddAsync(new DocumentOccurrence
                        {
                            DocumentId = src.SourceDocumentId,
                            OccurrenceType = OccurrenceTypeCatalog.MirrorFileMoved,
                            Source = OccurrenceSourceCatalog.System,
                            EventDate = now,
                            FileNumber = oldNumber,
                            FileType = oldType,
                            Year = oldYearNum,
                            FromCircuitName = sourceName,
                            ToCircuitName = targetName,
                            Details = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
                            {
                                ["fromCircuit"] = sourceName,
                                ["toCircuit"] = targetName,
                            }),
                            CreatedById = command.ActorUserId,
                            CreatedAt = now,
                            UpdatedAt = now,
                        }, token);
                    }
                }
                var occ = new DocumentOccurrence
                {
                    DocumentId = doc.Id,
                    OccurrenceType = OccurrenceTypeCatalog.CircuitReferred,
                    Source = OccurrenceSourceCatalog.System,
                    EventDate = now,
                    FileNumber = oldNumber,
                    FileType = oldType,
                    Year = oldYearNum,
                    FromCircuitName = sourceName,
                    ToCircuitName = targetName,
                    Details = System.Text.Json.JsonSerializer.Serialize(occDetails),
                    CreatedById = command.ActorUserId,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                await _occurrences.AddAsync(occ, token);
                referredAudit.Add((doc, before));
                referred++;
            }
            await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);

            // الواردة المعلقة بلا ملف مناب تُعاد توجيهها للدائرة الجديدة.
            var incoming = await _delegations.ListByDelegatedCircuitAsync(command.SourceCircuitId, token);
            var now2 = DateTime.UtcNow;
            foreach (var g in incoming.Where(g => g.Status == DelegationStatusCatalog.PendingHead))
            {
                g.DelegatedCircuitId = command.Request.TargetCircuitId;
                g.DelegatedCourt = target.Name;
                g.DelegatedCourtNorm = ArabicNameNormalizer.Normalize(target.Name);
                g.UpdatedAt = now2;
                _delegations.Update(g);
            }
            await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);

            // تدقيق حقلي لكل ملف محال (S5) — داخل المعاملة بعد الحفظ الرئيسي.
            foreach (var (doc, before) in referredAudit)
                await ExecutionCircuitRules.LogDocumentChangeAsync(_audit, doc, command.ActorName, "refer_circuit_files",
                    $"أحال الملف ({doc.Id}) من دائرة «{sourceName}» إلى دائرة «{targetName}» للمحامي {targetFullName}", before, token);

            await _audit.LogAsync(command.ActorName, "refer_circuit_files", null, null,
                $"أحال {referred} ملفًا من دائرة «{sourceName}» إلى دائرة «{targetName}» للمحامي {targetFullName} (متخطى {skipped})", token);

            // تنبيه المحامي المدمج (لكل محامٍ × دائرة) عبر Outbox داخل المعاملة.
            await ExecutionCircuitRules.EnqueuePendingAlertAsync(
                _alertOutbox,
                command.Request.TargetLawyerId,
                command.BranchId,
                command.ActorUserId,
                command.Request.TargetCircuitId,
                target.Name,
                now,
                token);
            await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);

            var counts = await _documents.CountByCircuitsAsync(new List<int> { command.SourceCircuitId }, token);
            remaining = counts.TryGetValue(command.SourceCircuitId, out var v) ? v.FileCount : 0;
        }, ct);

        return new ReferCircuitFilesResult(referred, skipped, remaining);
    }
}
