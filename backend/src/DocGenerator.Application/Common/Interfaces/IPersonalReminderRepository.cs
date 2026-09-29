using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

public interface IPersonalReminderRepository : IRepository<PersonalReminder>
{
    /// <summary>تذكيرات المالك مرتبة بتاريخ الاستحقاق — اختياريًا مع المؤرشفة.</summary>
    Task<List<PersonalReminder>> ListByLawyerAsync(int lawyerId, bool includeArchived, CancellationToken ct = default);

    /// <summary>عدد التذكيرات النشطة (غير المؤرشفة) — لسقف الإنشاء.</summary>
    Task<int> CountActiveAsync(int lawyerId, CancellationToken ct = default);
}
