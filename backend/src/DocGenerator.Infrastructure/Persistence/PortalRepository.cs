using System.Linq.Expressions;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// تنفيذ استعلامات بوابة مندوب الجهة: قاعدة الرؤية «أي تطابق طرفي بقيد نهائي»
/// (د1/د4) — قيود الانتظار لا تُدرج في النطاق حتى يعتمدها رئيس القسم.
/// فلاتر الحالة تحاكي قائمة الملفات (منفذ/تريث/تحت رفع) مع استبعاد المشطوب دائمًا.
/// </summary>
public class PortalRepository : IPortalRepository
{
    private readonly DocGeneratorDbContext _db;

    public PortalRepository(DocGeneratorDbContext db) => _db = db;

    /// <summary>
    /// قاعدة الرؤية الموحّدة (مصدر حقيقة واحد): ملف داخل النطاق إذا كان فيه أي
    /// طرف مرتبط بقيد نهائي ضمن معرّفات نطاق المندوب — تُستخدم في القائمة
    /// والتصدير وفحص التفاصيل معًا فلا يمكن أن يظهر ملف في القائمة ثم يُعاد 404 له.
    /// </summary>
    private static Expression<Func<Document, bool>> ScopePredicate(List<int> ids) => d =>
        d.ApplicantPublicEntities.Any(a => a.RegistryId != null
            && a.Registry != null
            && a.Registry.Status == EntityStatusCatalog.Final
            && ids.Contains(a.RegistryId.Value))
        || d.ExecutedPublicEntities.Any(e => e.EntityNature == PartyNatureCatalog.PublicEntity
            && e.RegistryId != null
            && e.Registry != null
            && e.Registry.Status == EntityStatusCatalog.Final
            && ids.Contains(e.RegistryId.Value));

    public Task<bool> IsDocumentInScopeAsync(int documentId, IReadOnlyCollection<int> entryIds, CancellationToken ct = default)
    {
        var ids = entryIds.ToList();
        return _db.Documents.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Where(ScopePredicate(ids))
            .AnyAsync(ct);
    }

    public async Task<(int TotalCount, List<Document> Items)> SearchScopedAsync(
        IReadOnlyCollection<int> entryIds, string? query, string? status,
        int page, int perPage, CancellationToken ct = default)
    {
        var baseQuery = ScopedQuery(entryIds, query, status);
        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Include(d => d.ApplicantPublicEntities)
            .Include(d => d.ExecutedPublicEntities)
            .ToListAsync(ct);
        return (total, items);
    }

    public Task<List<Document>> ExportScopedAsync(
        IReadOnlyCollection<int> entryIds, string? query, string? status, CancellationToken ct = default)
        => ScopedQuery(entryIds, query, status)
            .OrderByDescending(d => d.CreatedAt)
            .Include(d => d.Guarantors)
            .Include(d => d.Assets).ThenInclude(a => a.Owners)
            .Include(d => d.Heirs)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .Include(d => d.BaseNumbers)
            .Include(d => d.ExecutionApplicants)
            .Include(d => d.ExecutedPublicEntities)
            .Include(d => d.ExecutedNaturalPersons)
            .Include(d => d.ExecutedHeirs)
            .Include(d => d.Occurrences)
            .Include(d => d.Assignments)
            .Include(d => d.ApplicantPublicEntities)
            .AsSplitQuery()
            .ToListAsync(ct);

    public Task<int> CountScopedAsync(
        IReadOnlyCollection<int> entryIds, string? query, string? status, CancellationToken ct = default)
        => ScopedQuery(entryIds, query, status).CountAsync(ct);

    /// <inheritdoc />
    public async Task<PortalScopeResolution?> ResolveForUserAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking()
            .Include(u => u.PortalGroup).ThenInclude(g => g!.Entries)
            .Include(u => u.PortalEntry).ThenInclude(e => e!.Group)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null || user.Role != UserRole.EntityManager)
            return null;

        if (user.PortalGroupId.HasValue && user.PortalGroup is not null)
        {
            var group = user.PortalGroup;
            return new PortalScopeResolution(
                "group", group.Id, group.CanonicalName, group.EntityType,
                group.Entries
                    .Where(e => e.Status == EntityStatusCatalog.Final && e.IsActive)
                    .OrderBy(e => e.Governorate, StringComparer.Ordinal)
                    .ThenBy(e => e.BranchName, StringComparer.Ordinal)
                    .Select(e => (e.Id, e.Governorate, e.BranchName, e.IsActive))
                    .ToList());
        }

        if (user.PortalEntryId.HasValue && user.PortalEntry is not null)
        {
            var entry = user.PortalEntry;
            var active = entry.Status == EntityStatusCatalog.Final && entry.IsActive;
            return new PortalScopeResolution(
                "entry", entry.GroupId, entry.Group.CanonicalName, entry.Group.EntityType,
                active
                    ? new List<(int, string, string, bool)> { (entry.Id, entry.Governorate, entry.BranchName, true) }
                    : Array.Empty<(int, string, string, bool)>());
        }

        // بلا نطاق مضبوط: نطاق فارغ صريح — لا يرى المندوب شيئًا.
        return new PortalScopeResolution("group", 0, string.Empty, "ministry",
            Array.Empty<(int, string, string, bool)>());
    }

    // ── مساعدات خاصة ──

    /// <summary>بناء الاستعلام المقيّد بالفلاتر — يُترجم إلى SQL كاملًا.</summary>
    private IQueryable<Document> ScopedQuery(IReadOnlyCollection<int> entryIds, string? query, string? status)
    {
        // نسخة محلية ليُترجم Contains ضمن شجرة التعبير.
        var ids = entryIds.ToList();

        var q = _db.Documents.AsNoTracking()
            .Where(d => !d.IsDeleted)
            .Where(d => d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Where(ScopePredicate(ids));

        var term = query?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            q = q.Where(d => d.SearchText != null && d.SearchText.Contains(term));

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == ExecutionStatusCatalog.ExecutedFilter)
                q = q.Where(d => d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                    || d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement
                    || d.ExecStatus == ExecutionStatusCatalog.DelegationExecuted);
            else if (status == ExecutionStatusCatalog.Deferred)
                q = q.Where(d => d.ExecStatus == ExecutionStatusCatalog.Deferred);
            else if (status == ExecutionStatusCatalog.DraftFilter)
                q = q.Where(d => d.IsDraft);
            else if (status == ExecutionStatusCatalog.StateCirculating)
                q = q.Where(d => !d.IsDraft && string.IsNullOrEmpty(d.ExecStatus));
        }

        return q;
    }
}
