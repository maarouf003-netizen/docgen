using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// مستودع التذكيرات الشخصية: كل الاستعلامات مقيدة بمالك التذكير حصرًا —
/// لا يُكشف تذكير مستخدم لآخر على أي مستوى.
/// </summary>
public class PersonalReminderRepository : Repository<PersonalReminder>, IPersonalReminderRepository
{
    public PersonalReminderRepository(DocGeneratorDbContext db) : base(db) { }

    public async Task<List<PersonalReminder>> ListByLawyerAsync(int lawyerId, bool includeArchived, CancellationToken ct = default)
    {
        var query = Db.PersonalReminders
            .AsNoTracking()
            .Where(r => r.LawyerId == lawyerId);
        if (!includeArchived)
            query = query.Where(r => !r.IsArchived);
        return await query
            .OrderBy(r => r.DueDate)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);
    }

    public Task<int> CountActiveAsync(int lawyerId, CancellationToken ct = default)
        => Db.PersonalReminders
            .AsNoTracking()
            .CountAsync(r => r.LawyerId == lawyerId && !r.IsArchived, ct);
}
