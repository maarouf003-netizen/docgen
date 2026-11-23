using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Reminders;

/// <summary>إنشاء تذكير شخصي للمحامي — يملك معاملته وتدقيقه.</summary>
public sealed record CreatePersonalReminderCommand(
    CreatePersonalReminderRequest Request,
    int UserId,
    string? ActorName) : ICommand<PersonalReminderDto>, ITransactionalRequest;

public sealed class CreatePersonalReminderCommandHandler
    : CommandHandlerBase<CreatePersonalReminderCommand, PersonalReminderDto>
{
    private readonly IPersonalReminderRepository _reminders;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public CreatePersonalReminderCommandHandler(
        IPersonalReminderRepository reminders,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<CreatePersonalReminderCommand, PersonalReminderDto>> behaviors)
        : base(behaviors)
    {
        _reminders = reminders;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<PersonalReminderDto> HandleCoreAsync(
        CreatePersonalReminderCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var title = PersonalReminderService.ValidateTitle(request.Title);
        var notes = PersonalReminderService.ValidateNotes(request.Notes);
        var dueDate = PersonalReminderDates.ParseDay(request.DueDate, "تاريخ الاستحقاق");
        var color = PersonalReminderService.ValidateColor(request.Color);
        var recurrence = PersonalReminderService.ValidateRecurrence(request.Recurrence);
        DateTime? recurrenceEnd = request.RecurrenceEnd is null
            ? null
            : PersonalReminderDates.ParseDay(request.RecurrenceEnd, "تاريخ انتهاء التكرار");
        if (recurrenceEnd < dueDate)
            throw new ArgumentException("تاريخ انتهاء التكرار يجب أن يكون مساويًا لتاريخ الاستحقاق أو بعده");

        var activeCount = await _reminders.CountActiveAsync(command.UserId, ct);
        if (activeCount >= PersonalReminderCatalog.MaxActivePerUser)
            throw new ArgumentException($"تجاوزت الحد الأقصى للتذكيرات النشطة ({PersonalReminderCatalog.MaxActivePerUser}) — احذف القديم منها");

        var reminder = new PersonalReminder
        {
            LawyerId = command.UserId,
            Title = title,
            Notes = notes,
            DueDate = dueDate,
            Color = color,
            Recurrence = recurrence,
            RecurrenceEnd = recurrenceEnd,
            CreatedAt = DateTime.UtcNow,
        };

        await _reminders.AddAsync(reminder, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "personal-reminder.create",
            details: $"أنشأ تذكيرًا شخصيًا: {title}",
            ct: ct);

        return PersonalReminderService.ToDto(reminder);
    }
}
