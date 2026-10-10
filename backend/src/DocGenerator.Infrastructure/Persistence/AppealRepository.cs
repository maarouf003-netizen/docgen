using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// استعلامات الاستئنافات على مستوى قاعدة البيانات: الجلب بروابطه، قوائم الملف،
/// بحث نطاق الرؤية مع مطابقة لقطات الأطراف، وفحص متابعة المحامي المسند إليه.
/// </summary>
public class AppealRepository : Repository<DocumentAppeal>, IAppealRepository
{
    public AppealRepository(DocGeneratorDbContext db) : base(db) { }

    public Task<DocumentAppeal?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
        => WithIncludes(Db.DocumentAppeals).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<List<DocumentAppeal>> ListByDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var items = await WithIncludes(Db.DocumentAppeals.AsNoTracking())
            .Where(a => a.DocumentId == documentId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return items;
    }

    public async Task<(int Total, List<DocumentAppeal> Items)> SearchAsync(
        string? query,
        string? status,
        int? visibleBranchId,
        int? visibleUserId,
        int? ownerSectionId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var q = WithIncludes(Db.DocumentAppeals.AsNoTracking());

        if (visibleUserId is not null)
        {
            // محامٍ: الاستئنافات المسندة إليه للمتابعة فقط (R6) — المنشئ غير المسند لا يراها هنا.
            q = q.Where(a => a.AssignedLawyerId == visibleUserId.Value);
        }
        else if (visibleBranchId is not null)
        {
            q = q.Where(a => a.Document.BranchId == visibleBranchId.Value);
            if (ownerSectionId.HasValue)
            {
                // رئيس شعبة: استئنافات ملفات دوائر شعبته.
                q = q.Where(a => a.Document.ExecutionCircuitId != null
                    && a.Document.ExecutionCircuit!.SectionId == ownerSectionId.Value);
            }
            else
            {
                // رئيس قسم: استئنافات ملفات دوائر القسم وبلا دائرة + المحال له
                // استثناءً حتى الحسم (قرار §2.22 — المحسوم/المشطوب لا استثناء له).
                q = q.Where(a => a.Document.ExecutionCircuitId == null
                    || a.Document.ExecutionCircuit!.SectionId == null
                    || (a.Status == AppealStatusCatalog.Pending
                        && a.ForwardState == AppealForwardCatalog.ForwardedToHead));
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var term = status.Trim();
            q = q.Where(a => a.Status == term);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(a =>
                (a.AppellantsJson != null && a.AppellantsJson.Contains(term)) ||
                (a.AppelleesJson != null && a.AppelleesJson.Contains(term)) ||
                (a.AppealBaseNumber != null && a.AppealBaseNumber.Contains(term)) ||
                (a.AppellateCourt != null && a.AppellateCourt.Contains(term)) ||
                a.BaseNumbers.Any(b => b.BaseNumber != null && b.BaseNumber.Contains(term)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(ct);

        return (total, items);
    }

    public Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default)
        => Db.DocumentAppeals.AnyAsync(a => a.DocumentId == documentId && a.AssignedLawyerId == userId, ct);

    public Task<List<DocumentAppeal>> ListPendingByAssigneeInScopeAsync(
        int assigneeId, int branchId, int? ownerSectionId, CancellationToken ct = default)
    {
        // النقل الجملة للمنظورة ضمن نطاق المنفِّذ فقط (§5.5) — فلترة قاعدية
        // بلا تحميل التفاصيل (المحسوم والمشطوب وخارج النطاق يُستبعدان هنا).
        // بتتبّع لأن القصد تحديثها؛ لا روابط لازمة (الحقول المكتوبة عددية فقط).
        return InScope(Db.DocumentAppeals, assigneeId, branchId, ownerSectionId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<int> CountPendingByAssigneeInScopeAsync(
        int assigneeId, int branchId, int? ownerSectionId, CancellationToken ct = default)
    {
        // المعاينة بالنطاق نفسه (§5.5): تطابق المنقول فعلًا — عدّ قاعدي بلا جلب.
        return InScope(Db.DocumentAppeals.AsNoTracking(), assigneeId, branchId, ownerSectionId)
            .CountAsync(ct);
    }

    /// <summary>
    /// قيد النطاق المشترك للنقل الجملة ومعاينته: إسناد + فرع + منظور +
    /// ملكية الدائرة (شعبة المنفِّذ، أو القسم وبلا دائرة لرئيس القسم).
    /// </summary>
    private static IQueryable<DocumentAppeal> InScope(
        IQueryable<DocumentAppeal> source, int assigneeId, int branchId, int? ownerSectionId)
    {
        var q = source.Where(a =>
            a.AssignedLawyerId == assigneeId
            && a.Document.BranchId == branchId
            && a.Status == AppealStatusCatalog.Pending);
        return ownerSectionId.HasValue
            ? q.Where(a => a.Document.ExecutionCircuitId != null
                && a.Document.ExecutionCircuit!.SectionId == ownerSectionId.Value)
            : q.Where(a => a.Document.ExecutionCircuitId == null
                || a.Document.ExecutionCircuit!.SectionId == null
                // رئيس القسم: المحال له استثناءً حتى الحسم — مرآة البحث (قرار §2.22).
                || (a.Status == AppealStatusCatalog.Pending
                    && a.ForwardState == AppealForwardCatalog.ForwardedToHead));
    }
    public Task<int> CountByAssigneeAsync(int assigneeId, int? branchId = null, string? status = null, CancellationToken ct = default)
    {
        var q = Db.DocumentAppeals.AsNoTracking()
            .Where(a => a.AssignedLawyerId == assigneeId);
        if (branchId is not null)
            q = q.Where(a => a.Document.BranchId == branchId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(a => a.Status == status.Trim());
        return q.CountAsync(ct);
    }

    public async Task<List<DocumentAppeal>> ListByAssigneeAsync(
        int assigneeId, int? branchId = null, bool asNoTracking = true, CancellationToken ct = default)
    {
        var source = asNoTracking ? Db.DocumentAppeals.AsNoTracking() : Db.DocumentAppeals;
        var q = WithIncludes(source)
            .Where(a => a.AssignedLawyerId == assigneeId);
        if (branchId is not null)
            q = q.Where(a => a.Document.BranchId == branchId.Value);
        return await q
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<DocumentAppeal>> ListByDocumentIdsAsync(
        IReadOnlyCollection<int> documentIds, CancellationToken ct = default)
    {
        if (documentIds.Count == 0)
            return new List<DocumentAppeal>();

        return await Db.DocumentAppeals
            .Where(a => documentIds.Contains(a.DocumentId))
            .ToListAsync(ct);
    }

    /// <summary>
    /// الاستثناء القرائي للإحالة (قرار §2.22): هل على الملف إحالة مفتوحة لرئيس
    /// القسم (`ForwardedToHead`) — تُرى حتى الحسم (المحسوم/المشطوب لا استثناء له).
    /// </summary>
    public Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default)
        => Db.DocumentAppeals.AsNoTracking()
            .AnyAsync(a => a.DocumentId == documentId
                && a.Status == AppealStatusCatalog.Pending
                && a.ForwardState == AppealForwardCatalog.ForwardedToHead, ct);

    public async Task<Dictionary<int, int>> MapFirstAppealIdByDocumentIdsAsync(
        IReadOnlyCollection<int> documentIds, CancellationToken ct = default)
    {
        if (documentIds.Count == 0)
            return new Dictionary<int, int>();

        // شارة «استئناف» في قائمة الملفات التنفيذية تعني وجود استئناف منظور فقط:
        // المحسوم والمشطوب حالتان نهائيتان لا تتطلبان متابعة في القائمة (سجلهما يبقى
        // في صفحة الاستئنافات ووقوعات الملف)، فيُستبعدان هنا لتختفي الشارة بعد الحسم/الشطب.
        var rows = await Db.DocumentAppeals.AsNoTracking()
            .Where(a => documentIds.Contains(a.DocumentId) && a.Status == AppealStatusCatalog.Pending)
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.DocumentId })
            .ToListAsync(ct);

        var map = new Dictionary<int, int>();
        foreach (var row in rows)
        {
            if (!map.ContainsKey(row.DocumentId))
                map[row.DocumentId] = row.Id;
        }
        return map;
    }

    private static IQueryable<DocumentAppeal> WithIncludes(IQueryable<DocumentAppeal> q) =>
        q.Include(a => a.Document)
            .ThenInclude(d => d!.BaseNumbers)
            // الدائرة وشعبتها للتوجيه بالنطاق (§5 — قرار §2.21).
            .Include(a => a.Document)
                .ThenInclude(d => d!.ExecutionCircuit)
                .ThenInclude(c => c!.Section)
            .Include(a => a.AssignedLawyer)
            .Include(a => a.CreatedBy)
            .Include(a => a.BaseNumbers)
            .Include(a => a.Actions)
                .ThenInclude(ac => ac.CreatedBy);
}
