using DocGenerator.Application.Common.Interfaces;
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
            .Where(a => a.Recipients.Any(r => r.UserId == userId))
            .OrderByDescending(a => a.CreatedAt)
            .Include(a => a.CreatedBy)
            .Include(a => a.Document)
            .Include(a => a.TargetLawyer)
            .Include(a => a.Recipients)
            .ToListAsync(ct);
    }

    public async Task<List<HeadAlert>> ListByBranchAsync(int branchId, CancellationToken ct = default)
    {
        return await Db.HeadAlerts
            .AsNoTracking()
            .Where(a => a.BranchId == branchId)
            .OrderByDescending(a => a.CreatedAt)
            .Include(a => a.CreatedBy)
            .Include(a => a.Document)
            .Include(a => a.TargetLawyer)
            .Include(a => a.Recipients)
            .ToListAsync(ct);
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct = default)
        => Db.HeadAlertRecipients.CountAsync(r => r.UserId == userId && !r.IsRead, ct);

    public async Task<HeadAlert?> GetByIdWithRecipientsAsync(int id, CancellationToken ct = default)
    {
        return await Db.HeadAlerts
            .Include(a => a.Recipients)
            .Include(a => a.CreatedBy)
            .Include(a => a.Document)
            .Include(a => a.TargetLawyer)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<List<User>> ListActiveLawyersAsync(int branchId, CancellationToken ct = default)
    {
        return await Db.Users
            .Where(u => u.Role == UserRole.Lawyer && u.BranchId == branchId && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);
    }
}
