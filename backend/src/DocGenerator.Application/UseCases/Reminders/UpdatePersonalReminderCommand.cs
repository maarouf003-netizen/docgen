using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Reminders;

/// <summary>
/// تعديل تذكير شخصي (بما فيها الأرشفة الصريحة) — يملك معاملته وتدقيقه.
/// غير المالك يُعامل كغياب (`null`) دون كشف الوجود.
/// </summary>
public sealed record UpdatePersonalReminderCommand(
    int ReminderId,
    UpdatePersonalReminderRequest Request,
    int UserId,
    string? ActorName) : ICommand<PersonalReminderDto?>, ITransactionalRequest;

public sealed class UpdatePersonalReminderCommandHandler
    : CommandHandlerBase<UpdatePersonalReminderCommand, PersonalReminderDto?>
{
    private readonly IPersonalReminderRepository _reminders;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public UpdatePersonalReminderCommandHandler(
        IPersonalReminderRepository reminders,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<UpdatePersonalReminderCommand, PersonalReminderDto?>> behaviors)
        : base(behaviors)
    {
        _reminders = reminders;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<PersonalReminderDto?> HandleCoreAsync(
        UpdatePersonalReminderCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var reminder = await _reminders.GetByIdAsync(command.ReminderId, ct);
        if (reminder is null || reminder.LawyerId != command.UserId)
            return null;

        OptimisticConcurrencyGuard.EnsureCurrent(reminder, request.Version, "التذكير الشخصي");

        var changed = new List<string>();
        if (request.Title is not null)
        {
            reminder.Title = PersonalReminderService.ValidateTitle(request.Title);
            changed.Add("العنوان");
        }
        if (request.Notes is not null)
        {
            reminder.Notes = PersonalReminderService.ValidateNotes(request.Notes);
            changed.Add("الملاحظة");
        }
        if (request.DueDate is not null)
        {
            reminder.DueDate = PersonalReminderDates.ParseDay(request.DueDate, "تاريخ الاستحقاق");
            changed.Add("تاريخ الاستحقاق");
        }
        if (request.Color is not null)
        {
            reminder.Color = PersonalReminderService.ValidateColor(request.Color);
            changed.Add("اللون");
        }
        if (request.Recurrence is not null)
        {
            reminder.Recurrence = PersonalReminderService.ValidateRecurrence(request.Recurrence);
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
            return PersonalReminderService.ToDto(reminder);

        _reminders.Update(reminder);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "personal-reminder.update",
            details: $"عدّل تذكيره الشخصي «{reminder.Title}»: {string.Join("، ", changed)}",
            ct: ct);

        return PersonalReminderService.ToDto(reminder);
    }
}
