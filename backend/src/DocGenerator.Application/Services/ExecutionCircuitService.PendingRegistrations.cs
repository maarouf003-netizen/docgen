using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class ExecutionCircuitService : IExecutionCircuitService
{
    public async Task SyncPendingAlertsAfterTransferAsync(int sourceOwnerId, int targetOwnerId, int branchId, int actorUserId, CancellationToken ct = default)
    {
        // التنبيه يتبع المالك — تُنشأ تنبيهات المالك الجديد لدوائر معلقاته الحالية،
        // وتُصفَّى تنبيهات القديم التي لم يعد لها معلقات. تُنفَّذ داخل معاملة النقل
        // إن وُجدت (ذرّية مع تغيير الملكية)، وإلا تُفتح معاملة مستقلة.
        if (_tx is ITransactionContext { HasActiveTransaction: true })
        {
            await SyncPendingAlertsAfterTransferCoreAsync(
                sourceOwnerId, targetOwnerId, branchId, actorUserId, ct);
            return;
        }

        await _tx.RunAsync(token => SyncPendingAlertsAfterTransferCoreAsync(
            sourceOwnerId, targetOwnerId, branchId, actorUserId, token), ct);
    }

    private async Task SyncPendingAlertsAfterTransferCoreAsync(
        int sourceOwnerId, int targetOwnerId, int branchId, int actorUserId, CancellationToken token)
    {
        var targetPending = await _documents.ListPendingForLawyerAsync(targetOwnerId, null, token);
        foreach (var g in targetPending
                     .Where(d => d.ExecutionCircuitId.HasValue)
                     .GroupBy(d => d.ExecutionCircuitId!.Value))
        {
            var circuitName = g.Select(d => d.ExecutionCircuit?.Name)
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                ?? g.Select(d => d.Court)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
            if (string.IsNullOrWhiteSpace(circuitName))
                continue;
            await EnqueuePendingAlertAsync(
                targetOwnerId,
                branchId,
                actorUserId,
                g.Key,
                circuitName!,
                DateTime.UtcNow,
                token);
        }
        await CleanupPendingAlertsAsync(sourceOwnerId, token);
        await _uow.SaveChangesAsync(token);
        _cache?.Invalidate(ReferenceDataGroups.Circuits);
    }

    public async Task<List<PendingRegistrationDto>> MyPendingRegistrationsAsync(int lawyerId, int? circuitId, CancellationToken ct = default)
    {
        var docs = await _documents.ListPendingForLawyerAsync(lawyerId, circuitId, ct);
        if (docs.Count == 0) return new List<PendingRegistrationDto>();
        var referrals = await _documents.ListLastCircuitReferralsAsync(docs.Select(d => d.Id).ToList(), ct);
        var referralByDoc = referrals
            .GroupBy(o => o.DocumentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.Id).First());
        var circuits = await ListCircuitsAsync(ct);
        var circuitNames = circuits.ToDictionary(c => c.Id, c => c.Name);
        return docs.OrderBy(d => d.Id).Select(d =>
        {
            // المعروض حاليًا (قديم — من EffectiveFileIdentity).
            referralByDoc.TryGetValue(d.Id, out var lastReferred);
            string? circuitName = null;
            if (d.ExecutionCircuitId is not null)
                circuitNames.TryGetValue(d.ExecutionCircuitId.Value, out circuitName);
            return new PendingRegistrationDto(
                d.Id,
                d.ExecutionCircuitId ?? 0,
                circuitName ?? d.Court,
                d.BorrowerName,
                lastReferred?.FileNumber ?? d.FileNumber,
                lastReferred?.FileType ?? d.FileType,
                lastReferred?.Year?.ToString(CultureInfo.InvariantCulture) ?? d.FileYear);
        }).ToList();
    }

    public async Task<int> CompleteRegistrationsAsync(int lawyerId, string? actorName, CompleteRegistrationsRequest request, CancellationToken ct = default)
    {
        if (request.Entries is null || request.Entries.Count == 0)
            throw new ArgumentException("لا توجد ملفات لإعادة القيد");
        var ids = request.Entries.Select(e => e.DocumentId).Distinct().ToList();
        if (ids.Count != request.Entries.Count)
            throw new ArgumentException("تكرار في الملفات المرسلة");

        var saved = 0;
        try
        {
            await _tx.RunAsync(async token =>
            {
                var docs = await _documents.ListByIdsForUpdateAsync(ids, token);
                if (docs.Count != ids.Count)
                    throw new ArgumentException("أحد الملفات غير موجود");
                var byId = docs.ToDictionary(d => d.Id);
                // فحص الملكية + flag داخل المعاملة.
                foreach (var e in request.Entries)
                {
                    var doc = byId[e.DocumentId];
                    if (doc.IsDeleted)
                        throw new ArgumentException($"الملف ({e.DocumentId}) محذوف");
                    if (doc.CreatedById != lawyerId)
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
                            doc.Id, doc.ExecutionCircuitId, doc.CourtNorm, number, type, year, token))
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
                            doc, lawyerId, actorName, request.WarningToken, token);

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
                        CreatedById = lawyerId,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    await _occurrences.AddAsync(occ, token);
                    completedAudit.Add((doc, before, number, type, year));
                    saved++;
                }
                await _uow.SaveChangesAsync(token);
                _cache?.Invalidate(ReferenceDataGroups.Circuits);
                // تدقيق حقلي لكل ملف (S5) — داخل المعاملة بعد الحفظ الرئيسي.
                foreach (var (doc, before, number, type, year) in completedAudit)
                    await ExecutionCircuitRules.LogDocumentChangeAsync(_audit, doc, actorName, "complete_registrations",
                        $"أعاد قيد الملف ({doc.Id}) برقم {number}/{type}/{year}", before, token);
                await _audit.LogAsync(actorName, "complete_registrations", null, null,
                    $"أعاد قيد {saved} ملفًا بأرقام جديدة", token);

                // خطاف التنظيف DeleteByPendingRegistration: يُستدعى عند اكتمال إعادة قيد آخر ملف معلق للمحامي.
                await CleanupPendingAlertsAsync(lawyerId, token);
                await _uow.SaveChangesAsync(token);
                _cache?.Invalidate(ReferenceDataGroups.Circuits);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException("رقم الأساس مكرر — أُدخل أثناء الحفظ من مستخدم آخر، أعد المحاولة برقم مختلف", ex);
        }
        return saved;
    }

    /// <summary>
    /// خطاف التنظيف المسمّى DeleteByPendingRegistration (على نمط DeleteByDelegationAsync):
    /// يُصفّى تنبيه المحامي تلقائيًا عند الصفر (لكل دائرة)، ويتبع المالك عند إعادة الإسناد.
    /// </summary>
    public async Task<bool> DeleteByPendingRegistrationAsync(int lawyerId, CancellationToken ct = default)
    {
        var removed = false;
        await _tx.RunAsync(async token =>
        {
            removed = await CleanupPendingAlertsAsync(lawyerId, token);
            if (removed)
                await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);
        }, ct);
        return removed;
    }

    private async Task<bool> CleanupPendingAlertsAsync(int lawyerId, CancellationToken token)
    {
        var pending = await _documents.ListPendingForLawyerAsync(lawyerId, null, token);
        var lawyer = await _users.GetByIdAsync(lawyerId, token);
        var activeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var doc in pending)
        {
            if (doc.ExecutionCircuitId is null)
                continue;
            var docBranchId = doc.BranchId ?? lawyer?.BranchId;
            if (docBranchId is null)
                continue;
            activeKeys.Add(HeadAlertDedupe.Build(new HeadAlertDedupeScope
            {
                Kind = HeadAlertKindCatalog.CircuitPendingDocuments,
                Audience = HeadAlertAudience.SingleLawyer,
                BranchId = docBranchId.Value,
                RecipientUserId = lawyerId,
                ExecutionCircuitId = doc.ExecutionCircuitId,
            }));
        }

        var archived = await _alerts.ArchiveStalePendingByLawyerAsync(
            lawyerId, activeKeys, DateTime.UtcNow, token);
        return archived > 0;
    }
}
