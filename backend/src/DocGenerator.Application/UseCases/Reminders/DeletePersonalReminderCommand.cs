using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Reminders;

/// <summary>حذف تذكير شخصي يملكه المحامي — يملك معاملته وتدقيقه.</summary>
public sealed record DeletePersonalReminderCommand(
    int ReminderId,
    int UserId,
    string? ActorName) : ICommand<bool>, ITransactionalRequest;

public sealed class DeletePersonalReminderCommandHandler
    : CommandHandlerBase<DeletePersonalReminderCommand, bool>
{
    private readonly IPersonalReminderRepository _reminders;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public DeletePersonalReminderCommandHandler(
        IPersonalReminderRepository reminders,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<DeletePersonalReminderCommand, bool>> behaviors)
        : base(behaviors)
    {
        _reminders = reminders;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(DeletePersonalReminderCommand command, CancellationToken ct)
    {
        var reminder = await _reminders.GetByIdAsync(command.ReminderId, ct);
        if (reminder is null || reminder.LawyerId != command.UserId)
            return false;

        var title = reminder.Title;
        _reminders.Remove(reminder);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "personal-reminder.delete",
            details: $"حذف التذكير الشخصي: {title}", ct: ct);
        return true;
    }
}
