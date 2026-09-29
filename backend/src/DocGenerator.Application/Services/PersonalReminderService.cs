using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IPersonalReminderService
{
    Task<List<PersonalReminderDto>> ListAsync(int userId, bool includeArchived, CancellationToken ct = default);
    Task<PersonalReminderDto?> GetByIdAsync(int id, int userId, CancellationToken ct = default);
    Task<PersonalReminderDto> CreateAsync(CreatePersonalReminderRequest request, int userId, string? actorName, CancellationToken ct = default);
    Task<PersonalReminderDto?> UpdateAsync(int id, UpdatePersonalReminderRequest request, int userId, string? actorName, CancellationToken ct = default);
    Task<PersonalReminderDto?> SetOccurrenceAsync(int id, string occurrenceDate, bool done, int userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, int userId, string? actorName, CancellationToken ct = default);
}

/// <summary>
/// التذكيرات الشخصية الحرة للمحامي: كل عملية مقيدة بمالك التذكير (غير المالك
/// يُعامل كغياب — `null`/`false` — دون كشف الوجود)، والكتابة ضمن معاملة مع سجل التدقيق.
/// </summary>
public sealed class PersonalReminderService : IPersonalReminderService
{
    private readonly IPersonalReminderRepository _reminders;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public PersonalReminderService(
        IPersonalReminderRepository reminders,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _reminders = reminders;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

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

    public async Task<PersonalReminderDto> CreateAsync(
        CreatePersonalReminderRequest request, int userId, string? actorName, CancellationToken ct = default)
    {
        var title = ValidateTitle(request.Title);
        var notes = ValidateNotes(request.Notes);
        var dueDate = PersonalReminderDates.ParseDay(request.DueDate, "تاريخ الاستحقاق");
        var color = ValidateColor(request.Color);
        var recurrence = ValidateRecurrence(request.Recurrence);
        DateTime? recurrenceEnd = request.RecurrenceEnd is null
            ? null
            : PersonalReminderDates.ParseDay(request.RecurrenceEnd, "تاريخ انتهاء التكرار");
        if (recurrenceEnd < dueDate)
            throw new ArgumentException("تاريخ انتهاء التكرار يجب أن يكون مساويًا لتاريخ الاستحقاق أو بعده");

        var activeCount = await _reminders.CountActiveAsync(userId, ct);
        if (activeCount >= PersonalReminderCatalog.MaxActivePerUser)
            throw new ArgumentException($"تجاوزت الحد الأقصى للتذكيرات النشطة ({PersonalReminderCatalog.MaxActivePerUser}) — أرشف القديم منها");

        var reminder = new PersonalReminder
        {
            LawyerId = userId,
            Title = title,
            Notes = notes,
            DueDate = dueDate,
            Color = color,
            Recurrence = recurrence,
            RecurrenceEnd = recurrenceEnd,
            CreatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _reminders.AddAsync(reminder, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "personal-reminder.create",
                details: $"أنشأ تذكيرًا شخصيًا: {title}",
                ct: token);
        }, ct);

        return ToDto(reminder);
    }

    public async Task<PersonalReminderDto?> UpdateAsync(
        int id, UpdatePersonalReminderRequest request, int userId, string? actorName, CancellationToken ct = default)
    {
        var reminder = await _reminders.GetByIdAsync(id, ct);
        if (reminder is null || reminder.LawyerId != userId)
            return null;

        var changed = new List<string>();
        if (request.Title is not null)
        {
            reminder.Title = ValidateTitle(request.Title);
            changed.Add("العنوان");
        }
        if (request.Notes is not null)
        {
            reminder.Notes = ValidateNotes(request.Notes);
            changed.Add("الملاحظة");
        }
        if (request.DueDate is not null)
        {
            reminder.DueDate = PersonalReminderDates.ParseDay(request.DueDate, "تاريخ الاستحقاق");
            changed.Add("تاريخ الاستحقاق");
        }
        if (request.Color is not null)
        {
            reminder.Color = ValidateColor(request.Color);
            changed.Add("اللون");
        }
        if (request.Recurrence is not null)
        {
            reminder.Recurrence = ValidateRecurrence(request.Recurrence);
            changed.Add("التكرار");
        }
        if (request.RecurrenceEnd is not null)
        {
            reminder.RecurrenceEnd = string.IsNullOrWhiteSpace(request.RecurrenceEnd)
                ? null
                : PersonalReminderDates.ParseDay(request.RecurrenceEnd, "تاريخ انتهاء التكرار");
            changed.Add("انتهاء التكرار");
        }
        if (reminder.RecurrenceEnd < reminder.DueDate)
            throw new ArgumentException("تاريخ انتهاء التكرار يجب أن يكون مساويًا لتاريخ الاستحقاق أو بعده");
        if (request.IsArchived is not null && reminder.IsArchived != request.IsArchived.Value)
        {
            reminder.IsArchived = request.IsArchived.Value;
            changed.Add(request.IsArchived.Value ? "الأرشفة" : "إعادة التفعيل");
        }

        if (changed.Count == 0)
            return ToDto(reminder);

        await _tx.RunAsync(async token =>
        {
            _reminders.Update(reminder);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "personal-reminder.update",
                details: $"عدّل تذكيره الشخصي «{reminder.Title}»: {string.Join("، ", changed)}",
                ct: token);
        }, ct);

        return ToDto(reminder);
    }

    public async Task<PersonalReminderDto?> SetOccurrenceAsync(
        int id, string occurrenceDate, bool done, int userId, CancellationToken ct = default)
    {
        var reminder = await _reminders.GetByIdAsync(id, ct);
        if (reminder is null || reminder.LawyerId != userId)
            return null;
        if (reminder.IsArchived)
            throw new ArgumentException("التذكير مؤرشف — أعد تفعيله أولًا");

        // مفتاح التكرار يُطبَّع لصيغة اليوم (`yyyy-MM-dd`) عبر عقد التواريخ المعتمد.
        var key = PersonalReminderDates.Format(PersonalReminderDates.ParseDay(occurrenceDate, "تاريخ التكرار"));
        var keys = reminder.CompletedOccurrenceKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var mutated = done ? keys.Add(key) : keys.Remove(key);
        if (!mutated)
            return ToDto(reminder);

        reminder.CompletedOccurrenceKeys = string.Join(',', keys.OrderBy(k => k, StringComparer.Ordinal));
        if (reminder.CompletedOccurrenceKeys.Length > PersonalReminderCatalog.CompletedKeysMaxLength)
            throw new ArgumentException("تجاوز سجل التكرارات المنجزة حده — أرشف التذكير وأنشئ غيره");

        await _tx.RunAsync(async token =>
        {
            _reminders.Update(reminder);
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ToDto(reminder);
    }

    public async Task<bool> DeleteAsync(int id, int userId, string? actorName, CancellationToken ct = default)
    {
        var reminder = await _reminders.GetByIdAsync(id, ct);
        if (reminder is null || reminder.LawyerId != userId)
            return false;

        var title = reminder.Title;
        await _tx.RunAsync(async token =>
        {
            _reminders.Remove(reminder);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "personal-reminder.delete",
                details: $"حذف تذكيره الشخصي: {title}",
                ct: token);
        }, ct);
        return true;
    }

    private static string ValidateTitle(string? title)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("عنوان التذكير مطلوب");
        if (trimmed.Length > PersonalReminderCatalog.TitleMaxLength)
            throw new ArgumentException($"عنوان التذكير يتجاوز الحد الأقصى ({PersonalReminderCatalog.TitleMaxLength} حرفًا)");
        return trimmed;
    }

    private static string? ValidateNotes(string? notes)
    {
        var trimmed = notes?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > PersonalReminderCatalog.NotesMaxLength)
            throw new ArgumentException($"الملاحظة تتجاوز الحد الأقصى ({PersonalReminderCatalog.NotesMaxLength} حرفًا)");
        return trimmed;
    }

    private static string? ValidateColor(string? color)
    {
        var trimmed = color?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (!PersonalReminderCatalog.Colors.Contains(trimmed))
            throw new ArgumentException("لون التذكير غير صالح");
        return trimmed;
    }

    private static string ValidateRecurrence(string? recurrence)
    {
        var trimmed = recurrence?.Trim() ?? string.Empty;
        if (!PersonalReminderCatalog.Recurrences.Contains(trimmed))
            throw new ArgumentException("نوع التكرار غير صالح");
        return trimmed;
    }

    private static PersonalReminderDto ToDto(PersonalReminder r) => new(
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
        r.CreatedAt);
}
