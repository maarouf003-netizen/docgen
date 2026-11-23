using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public partial class HeadAlertRepository
{
    public Task<int> CountUnreadForUserAsync(
        int userId, int? branchId, CancellationToken ct = default)
        => Db.HeadAlertRecipients.CountAsync(
            r => r.UserId == userId
                && !r.IsRead
                && !r.HeadAlert!.IsArchived
                && (branchId == null || r.HeadAlert!.BranchId == branchId),
            ct);

    public async Task<HeadAlertDetailRow?> GetDetailRowAsync(
        int alertId, int actorUserId, CancellationToken ct = default)
    {
        // إسقاط واحد: لا Include(Recipients) ولا تحميل كيانات (الخطة §9.2).
        return await Db.HeadAlerts
            .AsNoTracking()
            .Where(a => a.Id == alertId)
            .Select(a => new HeadAlertDetailRow(
                a.Id,
                a.Kind,
                a.Message,
                a.TargetType,
                a.DocumentId,
                a.Document != null,
                a.Document != null ? a.Document.BorrowerName : null,
                a.Document != null ? a.Document.BorrowerFather : null,
                a.Document != null ? a.Document.BorrowerFamily : null,
                a.Document != null ? a.Document.DocumentType : null,
                a.TargetLawyerId,
                a.TargetLawyer != null ? a.TargetLawyer.FullName : null,
                a.DelegationId,
                a.AppealId,
                a.ReviewLetterId,
                a.PublicEntityId,
                a.ExecutionCircuitId,
                a.CreatedAt,
                a.LastContentChangedAt,
                a.IsArchived,
                a.ArchivedAt,
                a.CreatedBy != null ? a.CreatedBy.FullName : null,
                a.CreatedById,
                a.Recipients.Any(r => r.UserId == actorUserId),
                a.Recipients
                    .Where(r => r.UserId == actorUserId)
                    .Select(r => (bool?)r.IsRead)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> TryMarkReadAsync(
        int alertId, int userId, DateTime readAt, CancellationToken ct = default)
    {
        // تعبير SET واحد يترجم إلى UPDATE ذرّي (SQLite/PostgreSQL) ويترك أي كيان
        // متتبَّع في السياق على حاله — لا يُعتمد عليه في قرار القراءة.
        var updated = await Db.HeadAlertRecipients
            .Where(r => r.HeadAlertId == alertId && r.UserId == userId && !r.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsRead, true)
                .SetProperty(r => r.ReadAt, readAt), ct);
        if (updated > 0)
            return true;

        // صفر صفوف = إما مقروء سلفًا (نجاح idempotent) أو غير مستلم (404) — الفرق
        // يقتضي فحص الوجود، والسباق الوحيد المتبقي هو حذف الصف بينهما (يُرد 404).
        return await Db.HeadAlertRecipients
            .AnyAsync(r => r.HeadAlertId == alertId && r.UserId == userId, ct);
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

    /// <summary>
    /// حذف انتقائي (§6.2/§6.5): صفوف استلام مستلم واحد من تنبيهات استئناف، مع
    /// حذف التنبيهات الميتة (كل مستلميها هو) — بلا مساس بباقي المستلمين، وبلا
    /// حفظ (ضمن معاملة المتصل).
    /// </summary>
}
