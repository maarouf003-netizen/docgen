using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// استعلام المستخدمين بالاسم على مستوى قاعدة البيانات (بدل جلب كل المستخدمين).
/// </summary>
public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(DocGeneratorDbContext db) : base(db) { }

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim();
        return await Db.Users
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized.ToLower(), ct);
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

    public async Task<bool> UsernameExistsAsync(string username, int? excludeUserId, CancellationToken ct = default)
    {
        var normalized = username.Trim();
        return await Db.Users
            .AnyAsync(u =>
                u.Username.ToLower() == normalized.ToLower()
                && (excludeUserId == null || u.Id != excludeUserId), ct);
    }
}
