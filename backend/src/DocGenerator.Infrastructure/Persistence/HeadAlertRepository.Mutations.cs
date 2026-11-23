using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public partial class HeadAlertRepository
{
    public async Task<int> RemoveAppealRecipientAsync(int appealId, int userId, CancellationToken ct = default)
    {
        var alerts = await Db.HeadAlerts
            .Where(a => a.AppealId == appealId)
            .Include(a => a.Recipients)
            .ToListAsync(ct);
        var removed = 0;
        foreach (var alert in alerts)
        {
            var mine = alert.Recipients.Where(r => r.UserId == userId).ToList();
            if (mine.Count == 0)
                continue;
            foreach (var r in mine)
            {
                Db.HeadAlertRecipients.Remove(r);
                removed++;
            }
            if (alert.Recipients.Count == mine.Count)
                Db.HeadAlerts.Remove(alert);
        }
        return removed;
    }

    // ── مسارات النوع الدلالي (الخطة §6.4/§8.1) ──

    /// <inheritdoc />
    public async Task<HeadAlertReferenceKind?> FindMissingReferenceAsync(
        CreateSystemHeadAlertRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // الفرع إلزامي في كل تنبيه نظامي (مفتاح أجنبي في المخطط).
        if (!await Db.Branches.AsNoTracking().AnyAsync(b => b.Id == request.BranchId, ct))
            return HeadAlertReferenceKind.Branch;

        // مرشّحات الاستعلام العامة تُسقط الملفات المحذوفة (حذفًا ناعمًا) والإنابات
        // والاستئنافات التابعة لملف محذوف — فيُعدّ المرجع حينها مفقودًا.
        if (request.DocumentId is int documentId
            && !await Db.Documents.AsNoTracking().AnyAsync(d => d.Id == documentId, ct))
            return HeadAlertReferenceKind.Document;

        if (request.DelegationId is int delegationId
            && !await Db.DocumentDelegations.AsNoTracking().AnyAsync(d => d.Id == delegationId, ct))
            return HeadAlertReferenceKind.Delegation;

        if (request.AppealId is int appealId
            && !await Db.DocumentAppeals.AsNoTracking().AnyAsync(a => a.Id == appealId, ct))
            return HeadAlertReferenceKind.Appeal;

        if (request.ReviewLetterId is int reviewLetterId
            && !await Db.ReviewLetters.AsNoTracking().AnyAsync(l => l.Id == reviewLetterId, ct))
            return HeadAlertReferenceKind.ReviewLetter;

        if (request.PublicEntityId is int publicEntityId
            && !await Db.PublicEntities.AsNoTracking().AnyAsync(e => e.Id == publicEntityId, ct))
            return HeadAlertReferenceKind.PublicEntity;

        if (request.ExecutionCircuitId is int executionCircuitId
            && !await Db.ExecutionCircuits.AsNoTracking().AnyAsync(c => c.Id == executionCircuitId, ct))
            return HeadAlertReferenceKind.ExecutionCircuit;

        return null;
    }

    public async Task<HeadAlert?> FindLatestActiveByDedupeKeyAsync(string dedupeKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dedupeKey))
            return null;

        // متتبَّعة: المعالج يحدّث الصف (النص وزمن آخر تغيير) داخل معاملته.
        return await Db.HeadAlerts
            .Include(a => a.Recipients)
            .Where(a => a.DedupeKey == dedupeKey && !a.IsArchived)
            .OrderByDescending(a => a.LastContentChangedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<HeadAlertRecipient>> ListRecipientsWithUserAsync(
        int alertId, CancellationToken ct = default)
    {
        return await Db.HeadAlertRecipients
            .AsNoTracking()
            .Where(r => r.HeadAlertId == alertId)
            .Include(r => r.User)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
    }

    public async Task<int> ArchiveByScopeAndKindsAsync(
        int scopeId, HeadAlertScopeKind scopeKind, IReadOnlySet<string> kinds, int? excludeAlertId,
        CancellationToken ct = default)
    {
        if (kinds is null || kinds.Count == 0)
            throw new ArgumentException("مجموعة أنواع الأرشفة إلزامية", nameof(kinds));

        // حارس الاستثناء (الخطة §8.1): معرّف مستثنًى غير موجود أو مؤرشف = خطأ
        // استدعاء — يُرفض بدل الأرشفة الجماعية الصامتة.
        if (excludeAlertId.HasValue)
        {
            var excludedIsActive = await Db.HeadAlerts
                .AnyAsync(a => a.Id == excludeAlertId.Value && !a.IsArchived, ct);
            if (!excludedIsActive)
                throw new InvalidOperationException(
                    $"التنبيه المستثنى من الأرشفة ({excludeAlertId.Value}) غير موجود أو مؤرشف.");
        }

        var query = scopeKind == HeadAlertScopeKind.Delegation
            ? Db.HeadAlerts.Where(a => a.DelegationId == scopeId)
            : Db.HeadAlerts.Where(a => a.AppealId == scopeId);

        var kindList = kinds.ToList();
        var candidates = await query
            .Where(a => !a.IsArchived && kindList.Contains(a.Kind))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var archived = 0;
        foreach (var alert in candidates)
        {
            if (excludeAlertId.HasValue && alert.Id == excludeAlertId.Value)
                continue;

            alert.IsArchived = true;
            alert.ArchivedAt = now;
            archived++;
        }

        if (archived > 0)
            await Db.SaveChangesAsync(ct);

        return archived;
    }

    public async Task<List<HeadAlert>> ListActivePendingByCircuitAndKindAsync(
        int circuitId, string kind, CancellationToken ct = default)
    {
        // متتبَّعة: تُحدَّث نصوصها داخل معاملة المعالجة (إعادة التسمية).
        return await Db.HeadAlerts
            .Include(a => a.Recipients)
            .Where(a => !a.IsArchived && a.Kind == kind && a.ExecutionCircuitId == circuitId)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task<List<HeadAlert>> ListActiveByCircuitOrDocumentsAsync(
        int circuitId, IReadOnlyCollection<int> documentIds, CancellationToken ct = default)
    {
        var ids = documentIds.Distinct().ToList();
        return await Db.HeadAlerts
            .Where(a => !a.IsArchived
                     && (a.ExecutionCircuitId == circuitId
                         || (a.DocumentId != null && ids.Contains(a.DocumentId.Value))))
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task<int> ArchiveStalePendingByLawyerAsync(
        int lawyerId, IReadOnlySet<string> activeDedupeKeys, DateTime archivedAt, CancellationToken ct = default)
    {
        var keep = activeDedupeKeys ?? new HashSet<string>(StringComparer.Ordinal);
        var alerts = await Db.HeadAlerts
            .Where(a => !a.IsArchived
                     && a.Kind == HeadAlertKindCatalog.CircuitPendingDocuments
                     && a.Recipients.Any(r => r.UserId == lawyerId)
                     && (a.DedupeKey == null || !keep.Contains(a.DedupeKey)))
            .ToListAsync(ct);

        foreach (var alert in alerts)
        {
            alert.IsArchived = true;
            alert.ArchivedAt = archivedAt;
        }

        return alerts.Count;
    }
}
