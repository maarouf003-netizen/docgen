using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Reminders;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري لمعالجات قراءات التذكيرات الشخصية (`P4-01cu`): يحافظ على توقيعات
/// خدمة التذكيرات القديمة فوق سجل المعالجات — منطق الاختبارات كما هو.
/// </summary>
public static class PersonalReminderReadQueryExtensions
{
    public static Task<List<PersonalReminderDto>> ListAsync(
        this PersonalReminderReadHandlers handlers,
        int userId,
        bool includeArchived,
        CancellationToken ct = default)
        => handlers.List.HandleAsync(new ListPersonalRemindersQuery(userId, includeArchived), ct);

    public static Task<PersonalReminderDto?> GetByIdAsync(
        this PersonalReminderReadHandlers handlers,
        int id,
        int userId,
        CancellationToken ct = default)
        => handlers.Get.HandleAsync(new GetPersonalReminderQuery(id, userId), ct);
}
