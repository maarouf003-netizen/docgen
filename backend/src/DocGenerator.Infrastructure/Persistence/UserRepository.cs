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

    public async Task<bool> UsernameExistsAsync(string username, int? branchId, int? excludeUserId, CancellationToken ct = default)
    {
        var normalized = ArabicNameNormalizer.Normalize(username);
        return await Db.Users
            .AnyAsync(u =>
                u.Username == normalized
                && u.BranchId == branchId
                && (excludeUserId == null || u.Id != excludeUserId), ct);
    }
}
