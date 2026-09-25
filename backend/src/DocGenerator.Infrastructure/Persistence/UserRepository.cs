using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// استعلام المستخدمين بالاسم على مستوى قاعدة البيانات (بدل جلب كل المستخدمين).
/// الأسماء تُخزَّن مطبّعة بقاعدة <see cref="ArabicNameNormalizer"/>، لذا المقارنة = مباشرة.
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(DocGeneratorDbContext db) : base(db) { }

    public async Task<List<User>> FindByUsernameAllAsync(string username, CancellationToken ct = default)
    {
        var normalized = ArabicNameNormalizer.Normalize(username);
        return await Db.Users
            .Include(u => u.Branch)
            .Where(u => u.Username == normalized)
            .OrderBy(u => u.BranchId)
            .ToListAsync(ct);
    }

    public async Task<List<User>> ListLawyersAsync(int? branchId, CancellationToken ct = default)
    {
        IQueryable<User> q = Db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Lawyer)
            .Include(u => u.Branch);

        if (branchId.HasValue)
            q = q.Where(u => u.BranchId == branchId);

        return await q.OrderBy(u => u.FullName).ToListAsync(ct);
    }

    public async Task<List<User>> ListAllUsersAsync(CancellationToken ct = default)
    {
        return await Db.Users
            .AsNoTracking()
            .Include(u => u.Branch)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
    }

    /// <summary>حسابات مندوبي الجهات مع نطاقهم (الهوية/القيد) لشاشة إدارة المندوبين.</summary>
    public async Task<List<User>> ListEntityManagersAsync(CancellationToken ct = default)
    {
        return await Db.Users
            .AsNoTracking()
            .Include(u => u.PortalGroup)
            .Include(u => u.PortalEntry).ThenInclude(e => e!.Group)
            .Where(u => u.Role == UserRole.EntityManager)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
    }

    /// <summary>مندوبو الجهات ضمن مجموعة هويات أم محددة — متتبَّعة للتعديل (ترحيل النطاق).</summary>
    public async Task<List<User>> ListEntityManagersByGroupIdsAsync(
        IReadOnlyCollection<int> groupIds, CancellationToken ct = default)
    {
        if (groupIds is null || groupIds.Count == 0)
            return new List<User>();

        var idSet = groupIds as IReadOnlySet<int> ?? new HashSet<int>(groupIds);

        // معرّفات القيود (Entry) التابعة للهويات الأم المطلوبة، لتعيين مندوبي مستوى القيد.
        var entryIds = await Db.PublicEntities
            .Where(e => idSet.Contains(e.GroupId))
            .Select(e => (int?)e.Id)
            .ToListAsync(ct);

        return await Db.Users
            .Include(u => u.PortalEntry).ThenInclude(e => e!.Group)
            .Where(u => u.Role == UserRole.EntityManager
                && ((u.PortalGroupId != null && idSet.Contains(u.PortalGroupId.Value))
                    || (u.PortalEntryId != null && entryIds.Contains(u.PortalEntryId))))
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
    }

    /// <summary>مندوبو الجهات المرتبطون بقيد محدد عبر PortalEntryId — متتبَّعة للتعديل (طيّ قيد).</summary>
    public async Task<List<User>> ListEntityManagersByEntryIdAsync(int entryId, CancellationToken ct = default)
    {
        return await Db.Users
            .Include(u => u.PortalEntry).ThenInclude(e => e!.Group)
            .Where(u => u.Role == UserRole.EntityManager && u.PortalEntryId == entryId)
            .ToListAsync(ct);
    }

    public async Task<List<User>> SearchCorrespondenceTargetsAsync(
        int excludeUserId, string? q, int limit, CancellationToken ct = default)
    {
        IQueryable<User> query = Db.Users
            .AsNoTracking()
            .Include(u => u.Branch)
            .Include(u => u.PortalEntry)
            .Where(u => u.IsActive
                && u.Id != excludeUserId
                && (u.Role == UserRole.Lawyer
                    || u.Role == UserRole.Head
                    || u.Role == UserRole.EntityManager));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.FullName.Contains(term));
        }

        return await query
            .OrderBy(u => u.FullName)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(ct);
    }

    public async Task<bool> UsernameExistsAsync(string username, int? branchId, int? excludeUserId, CancellationToken ct = default)
    {
        var normalized = ArabicNameNormalizer.Normalize(username);
        return await Db.Users
            .AnyAsync(u =>
                u.Username == normalized
                && u.BranchId == branchId
                && (excludeUserId == null || u.Id != excludeUserId), ct);
    }

    /// <inheritdoc />
    public async Task<List<User>> SearchDocumentLawyerTargetsAsync(
        int documentId, int? ownerUserId, int excludeUserId, string? q, int limit,
        CancellationToken ct = default)
    {
        IQueryable<User> query = Db.Users
            .AsNoTracking()
            .Include(u => u.Branch)
            .Where(u => u.Role == UserRole.Lawyer
                && u.IsActive
                && u.Id != excludeUserId
                && ((ownerUserId != null && u.Id == ownerUserId)
                    || Db.DocumentAppeals.Any(a => a.DocumentId == documentId
                        && a.AssignedLawyerId == u.Id)
                    || Db.DocumentDelegations.Any(d => d.SourceDocumentId == documentId
                        && d.AssignedLawyerId == u.Id)
                    || Db.Documents.Any(inner => inner.Id == documentId
                        && inner.SourceDelegation != null
                        && inner.SourceDelegation.AssignedLawyerId == u.Id)));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.FullName.Contains(term));
        }

        return await query
            .OrderBy(u => u.FullName)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<User>> SearchScopeDelegateTargetsAsync(
        IReadOnlyCollection<int> entryIds, IReadOnlyCollection<int> groupIds,
        int excludeUserId, string? q, int limit, CancellationToken ct = default)
    {
        if ((entryIds is null || entryIds.Count == 0) && (groupIds is null || groupIds.Count == 0))
            return new List<User>();

        var entrySet = entryIds as IReadOnlySet<int> ?? new HashSet<int>(entryIds);
        var groupSet = groupIds as IReadOnlySet<int> ?? new HashSet<int>(groupIds);

        IQueryable<User> query = Db.Users
            .AsNoTracking()
            .Include(u => u.Branch)
            .Include(u => u.PortalEntry)
            .Where(u => u.Role == UserRole.EntityManager
                && u.IsActive
                && u.Id != excludeUserId
                && ((u.PortalGroupId != null && groupSet.Contains(u.PortalGroupId.Value))
                    || (u.PortalEntryId != null && entrySet.Contains(u.PortalEntryId.Value))));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.FullName.Contains(term));
        }

        return await query
            .OrderBy(u => u.FullName)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> IsDocumentLawyerTargetAsync(
        int documentId, int? ownerUserId, int userId, CancellationToken ct = default)
        => Db.Users.AnyAsync(u => u.Id == userId && u.Role == UserRole.Lawyer && u.IsActive
            && ((ownerUserId != null && u.Id == ownerUserId)
                || Db.DocumentAppeals.Any(a => a.DocumentId == documentId
                    && a.AssignedLawyerId == u.Id)
                || Db.DocumentDelegations.Any(d => d.SourceDocumentId == documentId
                    && d.AssignedLawyerId == u.Id)
                || Db.Documents.Any(inner => inner.Id == documentId
                    && inner.SourceDelegation != null
                    && inner.SourceDelegation.AssignedLawyerId == u.Id)), ct);

    /// <inheritdoc />
    public Task<bool> IsScopeDelegateTargetAsync(
        IReadOnlyCollection<int> entryIds, IReadOnlyCollection<int> groupIds,
        int userId, CancellationToken ct = default)
    {
        var entrySet = entryIds as IReadOnlySet<int> ?? new HashSet<int>(entryIds);
        var groupSet = groupIds as IReadOnlySet<int> ?? new HashSet<int>(groupIds);
        return Db.Users.AnyAsync(u => u.Id == userId && u.Role == UserRole.EntityManager && u.IsActive
            && ((u.PortalGroupId != null && groupSet.Contains(u.PortalGroupId.Value))
                || (u.PortalEntryId != null && entrySet.Contains(u.PortalEntryId.Value))), ct);
    }
}
