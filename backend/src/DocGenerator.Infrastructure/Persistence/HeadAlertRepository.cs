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

    public async Task<List<User>> ListActiveHeadsAsync(int branchId, CancellationToken ct = default)
    {
        return await Db.Users
            .Where(u => u.Role == UserRole.Head && u.BranchId == branchId && u.IsActive)
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
        var rows = await Db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Head && u.BranchId != null && u.IsActive)
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

    public async Task<List<HeadAlert>> ListByAppealAsync(int appealId, CancellationToken ct = default)
    {
        // تتبُّع مفعّل: تُحذف هذه الكيانات عبر Remove فتُعاد للمُغيّر ذاتها (نمط ListByDelegationAsync).
        return await Db.HeadAlerts
            .Where(a => a.AppealId == appealId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<HeadAlert?> FindLatestUnseenByReviewLetterAsync(
        int reviewLetterId, int recipientUserId, CancellationToken ct = default)
    {
        // متتبَّعة عمدًا: ستُحدَّث رسالتها وزمنها لدمج الردود المتتالية في تنبيه واحد.
        return await Db.HeadAlerts
            .Include(a => a.Recipients)
            .Where(a => a.ReviewLetterId == reviewLetterId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.Recipients.Any(r => r.UserId == recipientUserId && !r.IsRead), ct);
    }

    public async Task<HeadAlert?> FindLatestPendingByEntityAsync(
        int publicEntityId, int recipientUserId, CancellationToken ct = default)
    {
        // متتبَّعة عمدًا: ستُحدَّث رسالتها وزمنها لدمج اقتراحات تعديل الجهة المتلاحقة
        // قبل الاعتماد في تنبيه واحد بآخر تعديل.
        return await Db.HeadAlerts
            .Include(a => a.Recipients)
            .Where(a => a.PublicEntityId == publicEntityId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.Recipients.Any(r => r.UserId == recipientUserId && !r.IsRead), ct);
    }
}