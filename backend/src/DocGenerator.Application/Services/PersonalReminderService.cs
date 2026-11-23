using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IPersonalReminderService
{
    Task<List<PersonalReminderDto>> ListAsync(int userId, bool includeArchived, CancellationToken ct = default);
    Task<PersonalReminderDto?> GetByIdAsync(int id, int userId, CancellationToken ct = default);
}

/// <summary>
/// التذكيرات الشخصية الحرة للمحامي: كل عملية مقيدة بمالك التذكير (غير المالك
/// يُعامل كغياب — `null`/`false` — دون كشف الوجود)، والكتابة ضمن معاملة مع سجل التدقيق.
/// الإنشاء/التعديل مملوكان لـ`CreatePersonalReminderCommand`/`UpdatePersonalReminderCommand`؛
/// هذه الخدمة للقراءة والتحقق/الإسقاط المشتركين.
/// </summary>
public sealed class PersonalReminderService : IPersonalReminderService
{
    private readonly IPersonalReminderRepository _reminders;

    public PersonalReminderService(IPersonalReminderRepository reminders)
        => _reminders = reminders;

    public async Task<List<PersonalReminderDto>> ListAsync(int userId, bool includeArchived, CancellationToken ct = default)
    {
        var reminders = await _reminders.ListByLawyerAsync(userId, includeArchived, ct);
        return reminders.Select(ToDto).ToList();
    }

    /// <summary>
    /// جلب مباشر بالمعرف مع فحص الملكية — غير المالك يُعامل كغياب (`null`)
    /// دون كشف الوجود، وبذلك لا حاجة لجلب القائمة كاملة ثم البحث فيها.
    /// </summary>
    public async Task<PersonalReminderDto?> GetByIdAsync(int id, int userId, CancellationToken ct = default)
    {
        var reminder = await _reminders.GetByIdAsync(id, ct);
        if (reminder is null || reminder.LawyerId != userId)
            return null;
        return ToDto(reminder);
    }

    internal static string ValidateTitle(string? title)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("عنوان التذكير مطلوب");
        if (trimmed.Length > PersonalReminderCatalog.TitleMaxLength)
            throw new ArgumentException($"عنوان التذكير يتجاوز الحد الأقصى ({PersonalReminderCatalog.TitleMaxLength} حرفًا)");
        return trimmed;
    }

    internal static string? ValidateNotes(string? notes)
    {
        var trimmed = notes?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > PersonalReminderCatalog.NotesMaxLength)
            throw new ArgumentException($"الملاحظة تتجاوز الحد الأقصى ({PersonalReminderCatalog.NotesMaxLength} حرفًا)");
        return trimmed;
    }

    internal static string? ValidateColor(string? color)
    {
        var trimmed = color?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (!PersonalReminderCatalog.Colors.Contains(trimmed))
            throw new ArgumentException("لون التذكير غير صالح");
        return trimmed;
    }

    internal static string ValidateRecurrence(string? recurrence)
    {
        var trimmed = recurrence?.Trim() ?? string.Empty;
        if (!PersonalReminderCatalog.Recurrences.Contains(trimmed))
            throw new ArgumentException("نوع التكرار غير صالح");
        return trimmed;
    }

    internal static PersonalReminderDto ToDto(PersonalReminder r) => new(
        r.Id,
        r.Title,
        r.Notes,
        PersonalReminderDates.Format(r.DueDate),
        r.Color,
        r.Recurrence,
        r.RecurrenceEnd is null ? null : PersonalReminderDates.Format(r.RecurrenceEnd.Value),
        r.IsArchived,
        r.CompletedOccurrenceKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => k.Length > 0)
            .ToList(),
        r.CreatedAt,
        r.Version);
}
