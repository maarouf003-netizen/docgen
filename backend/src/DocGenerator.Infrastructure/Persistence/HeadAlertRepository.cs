using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// استعلامات تنبيهات رئيس القسم: قوائم المستلم/الفرع وعدّاد غير المقروء ومستلمو التعميم.
/// </summary>
public class HeadAlertRepository : Repository<HeadAlert>, IHeadAlertRepository
{
    public HeadAlertRepository(DocGeneratorDbContext db) : base(db) { }

    public async Task<List<HeadAlert>> ListForRecipientAsync(int userId, CancellationToken ct = default)
    {
        return await Db.HeadAlerts
            .AsNoTracking()
            .Where(a => !a.IsArchived && a.Recipients.Any(r => r.UserId == userId))
            .OrderByDescending(a => a.CreatedAt)
            .Include(a => a.CreatedBy)
            .Include(a => a.Document)
            .Include(a => a.TargetLawyer)
            .Include(a => a.Recipients)
            .ToListAsync(ct);
    }

    /// <summary>
    /// تنبيهات مستلم في فرع (§8: قراءة بالمستلم لا بالفرع) — لرؤساء النطاق.
    /// </summary>
    public async Task<List<HeadAlert>> ListByRecipientInBranchAsync(int userId, int branchId, CancellationToken ct = default)
    {
        return await Db.HeadAlerts
            .AsNoTracking()
            .Where(a => !a.IsArchived && a.BranchId == branchId && a.Recipients.Any(r => r.UserId == userId))
            .OrderByDescending(a => a.CreatedAt)
            .Include(a => a.CreatedBy)
            .Include(a => a.Document)
            .Include(a => a.TargetLawyer)
            .Include(a => a.Recipients)
            .ToListAsync(ct);
    }

    public async Task<List<HeadAlert>> ListSentAsync(int userId, int branchId, CancellationToken ct = default)
    {
        return await Db.HeadAlerts
            .AsNoTracking()
            .Where(a => !a.IsArchived && a.BranchId == branchId && a.CreatedById == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Include(a => a.CreatedBy)
            .Include(a => a.Document)
            .Include(a => a.TargetLawyer)
            .Include(a => a.Recipients)
            .ToListAsync(ct);
    }

    public async Task<List<HeadAlertDto>> ListForOversightAsync(CancellationToken ct = default)
    {
        // قائمة إشرافية محدودة بإسقاط عرضي: ترتيب حتمي عند تساوي الطوابع،
        // والعدّادات استعلامات فرعية في قاعدة البيانات — لا تُحمَّل صفوف
        // المستلمين أصلًا. التحويلات غير القابلة للترجمة (نوع الهدف/العنوان)
        // تتم في الذاكرة على الصفوف المسقطة فقط.
        var rows = await Db.HeadAlerts
            .AsNoTracking()
            .Where(a => !a.IsArchived)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(500)
            .Select(a => new OversightAlertRow(
                a.Id,
                a.Message,
                a.TargetType,
                a.DocumentId,
                a.Document != null ? a.Document.BorrowerName : null,
                a.Document != null ? a.Document.BorrowerFather : null,
                a.Document != null ? a.Document.BorrowerFamily : null,
                a.Document != null ? a.Document.DocumentType : null,
                a.TargetLawyerId,
                a.TargetLawyer != null ? a.TargetLawyer.FullName : null,
                a.Recipients.Count(),
                a.Recipients.Count(r => !r.IsRead),
                a.CreatedAt,
                a.CreatedBy != null ? a.CreatedBy.FullName : null,
                a.AppealId,
                a.ReviewLetterId,
                a.DelegationId))
            .ToListAsync(ct);

        return rows.Select(r => new HeadAlertDto(
            r.Id,
            r.Message,
            r.TargetType.ToString().ToLowerInvariant(),
            r.DocumentId,
            OversightDocumentTitle(r),
            r.TargetLawyerId,
            r.TargetLawyerName,
            null,
            r.RecipientCount,
            r.UnreadCount,
            r.CreatedAt,
            r.CreatedByName,
            r.AppealId,
            r.ReviewLetterId,
            r.DelegationId)).ToList();
    }

    /// <summary>صف وسيط مسقط لقائمة الإشراف — نفس منطق عنوان المستند المطبق في الخدمة.</summary>
    private sealed record OversightAlertRow(
        int Id,
        string Message,
        HeadAlertTargetType TargetType,
        int? DocumentId,
        string? BorrowerName,
        string? BorrowerFather,
        string? BorrowerFamily,
        string? DocumentType,
        int? TargetLawyerId,
        string? TargetLawyerName,
        int RecipientCount,
        int UnreadCount,
        DateTime CreatedAt,
        string? CreatedByName,
        int? AppealId,
        int? ReviewLetterId,
        int? DelegationId);

    private static string OversightDocumentTitle(OversightAlertRow row)
    {
        if (row.BorrowerName is null && row.BorrowerFather is null
            && row.BorrowerFamily is null && row.DocumentType is null)
            return row.DocumentId is null ? string.Empty : "مستند محذوف";

        var parts = new[] { row.BorrowerName, row.BorrowerFather, row.BorrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var name = parts.Any() ? string.Join(" ", parts) : row.DocumentType;
        return string.IsNullOrWhiteSpace(name) ? $"مستند {row.DocumentId}" : name!;
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct = default)
        => Db.HeadAlertRecipients.CountAsync(
            r => r.UserId == userId && !r.IsRead && !r.HeadAlert!.IsArchived, ct);

    public async Task<HeadAlert?> GetByIdWithRecipientsAsync(int id, CancellationToken ct = default)
    {
        return await Db.HeadAlerts
            .Include(a => a.Recipients)
            .Include(a => a.CreatedBy)
            .Include(a => a.Document)
            .Include(a => a.TargetLawyer)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<List<HeadAlert>> ListActiveUnreadByRecipientWithRecipientsAsync(
        int userId, CancellationToken ct = default)
    {
        // متتبَّعة: التعاقب يعدّل صفوف الاستلام ومفاتيح المنع داخل معاملة المستخدم.
        // الفلترة على مستوى الاستعلام تُبقي المؤرشف والمقروء خارج النقل التاريخي.
        return await Db.HeadAlerts
            .Where(a => !a.IsArchived && a.Recipients.Any(r => r.UserId == userId && !r.IsRead))
            .Include(a => a.Recipients)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task<List<User>> ListActiveLawyersAsync(int branchId, CancellationToken ct = default)
    {
        return await Db.Users
            .Where(u => u.Role == UserRole.Lawyer && u.BranchId == branchId && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
    }

    public async Task<List<User>> ListActiveHeadsAsync(int branchId, CancellationToken ct = default)
    {
        // F3: رؤساء الأقسام والشعب المفعّلون (تماثل §2) — وعند غياب الجميع يُرفض الإنشاء
        // ويُسجَّل فشل الإشعار في سجل التدقيق (سلوك الغياب محفوظ).
        return await Db.Users
            .Where(u => (u.Role == UserRole.Head || u.Role == UserRole.SubHead) && u.BranchId == branchId && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
    }

    public async Task<List<(int BranchId, List<User> Lawyers)>> ListAllActiveLawyersGroupedByBranchAsync(CancellationToken ct = default)
    {
        var rows = await Db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Lawyer && u.BranchId != null && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { BranchId = u.BranchId!.Value, u.Id, u.FullName, u.Username, u.Email, u.Role, u.IsActive, u.CreatedAt, u.UpdatedAt })
            .ToListAsync(ct);

        var result = new List<(int BranchId, List<User> Lawyers)>();
        foreach (var group in rows.GroupBy(x => x.BranchId))
        {
            var lawyers = group.Select(g => new User
            {
                Id = g.Id,
                FullName = g.FullName,
                Username = g.Username,
                Email = g.Email,
                Role = g.Role,
                IsActive = g.IsActive,
                BranchId = g.BranchId,
                CreatedAt = g.CreatedAt,
                UpdatedAt = g.UpdatedAt,
            }).ToList();
            result.Add((group.Key, lawyers));
        }
        return result;
    }

    public async Task<List<(int BranchId, List<User> Heads)>> ListAllActiveHeadsGroupedByBranchAsync(CancellationToken ct = default)
    {
        // F3: رؤساء الأقسام والشعب (تماثل §2) — كل تنبيه لفرع المستلم حصرًا (بلا تسريب).
        var rows = await Db.Users
            .AsNoTracking()
            .Where(u => (u.Role == UserRole.Head || u.Role == UserRole.SubHead) && u.BranchId != null && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { BranchId = u.BranchId!.Value, u.Id, u.FullName, u.Username, u.Email, u.Role, u.IsActive, u.CreatedAt, u.UpdatedAt })
            .ToListAsync(ct);

        var result = new List<(int BranchId, List<User> Heads)>();
        foreach (var group in rows.GroupBy(x => x.BranchId))
        {
            var heads = group.Select(g => new User
            {
                Id = g.Id,
                FullName = g.FullName,
                Username = g.Username,
                Email = g.Email,
                Role = g.Role,
                IsActive = g.IsActive,
                BranchId = g.BranchId,
                CreatedAt = g.CreatedAt,
                UpdatedAt = g.UpdatedAt,
            }).ToList();
            result.Add((group.Key, heads));
        }
        return result;
    }

    public async Task<List<HeadAlert>> ListByDelegationAsync(int delegationId, CancellationToken ct = default)
    {
        // تتبُّع مفعّل: تُحذف هذه الكيانات عبر Remove فتُعاد للمُغيّر ذاتها (لا نسخًا منفصلة
        // تصطدم مع النسخ المتتبعة في ChangeTracker).
        return await Db.HeadAlerts
            .Where(a => a.DelegationId == delegationId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<HeadAlert?> FindLatestByDelegationAsync(int delegationId, CancellationToken ct = default)
    {
        return await Db.HeadAlerts
            .Where(a => a.DelegationId == delegationId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<HeadAlert?> FindLatestByDelegationAndRecipientAsync(
        int delegationId, int recipientLawyerId, CancellationToken ct = default)
    {
        // متتبَّعة عمدًا: يُحدَّث نُصّها وزمنها لدمج تغييرات المرآة المتعاقبة في تنبيه واحد
        // للمستلم نفسه.
        return await Db.HeadAlerts
            .Where(a => a.DelegationId == delegationId && a.TargetLawyerId == recipientLawyerId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<HeadAlert>> ListByDelegationWithRecipientsAsync(int delegationId, CancellationToken ct = default)
    {
        return await Db.HeadAlerts
            .AsNoTracking()
            .Where(a => a.DelegationId == delegationId && !a.IsArchived)
            .OrderByDescending(a => a.CreatedAt)
            .Include(a => a.CreatedBy)
            .Include(a => a.TargetLawyer)
            .Include(a => a.Recipients)
            .ToListAsync(ct);
    }

    public async Task<List<HeadAlert>> ListByAppealAsync(int appealId, CancellationToken ct = default)
    {
        // تتبُّع مفعّل: تُحذف هذه الكيانات عبر Remove فتُعاد للمُغيّر ذاتها (نمط ListByDelegationAsync).
        return await Db.HeadAlerts
            .Where(a => a.AppealId == appealId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>
    /// حذف انتقائي (§6.2/§6.5): صفوف استلام مستلم واحد من تنبيهات استئناف، مع
    /// حذف التنبيهات الميتة (كل مستلميها هو) — بلا مساس بباقي المستلمين، وبلا
    /// حفظ (ضمن معاملة المتصل).
    /// </summary>
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

    public async Task<List<HeadAlert>> ListActiveByScopeAndKindsAsync(
        int scopeId, HeadAlertScopeKind scopeKind, IReadOnlySet<string> kinds, CancellationToken ct = default)
    {
        if (kinds is null || kinds.Count == 0)
            return new List<HeadAlert>();

        var query = scopeKind == HeadAlertScopeKind.Delegation
            ? Db.HeadAlerts.Where(a => a.DelegationId == scopeId)
            : Db.HeadAlerts.Where(a => a.AppealId == scopeId);

        var kindList = kinds.ToList();
        return await query
            .Where(a => !a.IsArchived && kindList.Contains(a.Kind))
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task<List<HeadAlertRecipient>> ListRecipientsAsync(int alertId, CancellationToken ct = default)
    {
        return await Db.HeadAlertRecipients
            .AsNoTracking()
            .Where(r => r.HeadAlertId == alertId)
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

    public async Task<List<HeadAlert>> ListActivePendingByLawyerAndKindAsync(
        int lawyerId, string kind, CancellationToken ct = default)
    {
        return await Db.HeadAlerts
            .AsNoTracking()
            .Where(a => !a.IsArchived && a.Kind == kind
                     && a.Recipients.Any(r => r.UserId == lawyerId))
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
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
