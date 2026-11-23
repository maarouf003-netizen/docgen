using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Reminders;

/// <summary>تعليم/إلغاء إنجاز موعد تذكير شخصي — يملك معاملته.</summary>
public sealed record SetPersonalReminderOccurrenceCommand(
    int ReminderId,
    string OccurrenceDate,
    bool Done,
    int UserId,
    long? Version = null) : ICommand<PersonalReminderDto?>, ITransactionalRequest;

public sealed class SetPersonalReminderOccurrenceCommandHandler
    : CommandHandlerBase<SetPersonalReminderOccurrenceCommand, PersonalReminderDto?>
{
    private readonly IPersonalReminderRepository _reminders;
    private readonly IUnitOfWork _uow;

    public SetPersonalReminderOccurrenceCommandHandler(
        IPersonalReminderRepository reminders,
        IUnitOfWork uow,
        IEnumerable<IPipelineBehavior<SetPersonalReminderOccurrenceCommand, PersonalReminderDto?>> behaviors)
        : base(behaviors)
    {
        _reminders = reminders;
        _uow = uow;
    }

    protected override async Task<PersonalReminderDto?> HandleCoreAsync(
        SetPersonalReminderOccurrenceCommand command, CancellationToken ct)
    {
        var reminder = await _reminders.GetByIdAsync(command.ReminderId, ct);
        if (reminder is null || reminder.LawyerId != command.UserId)
            return null;

        OptimisticConcurrencyGuard.EnsureCurrent(reminder, command.Version, "التذكير الشخصي");
        if (reminder.IsArchived)
            throw new ArgumentException("التذكير مؤرشف — لا يمكن تعليم مواعيده كمنجزة");

        // مفتاح الموعد يُطبَّع إلى صيغة اليوم (`yyyy-MM-dd`) قبل أي مقارنة.
        var key = PersonalReminderDates.Format(
            PersonalReminderDates.ParseDay(command.OccurrenceDate, "تاريخ الموعد"));
        var keys = reminder.CompletedOccurrenceKeys
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var mutated = command.Done ? keys.Add(key) : keys.Remove(key);
        if (!mutated)
            return PersonalReminderService.ToDto(reminder);

        reminder.CompletedOccurrenceKeys = string.Join(',', keys.OrderBy(k => k, StringComparer.Ordinal));
        if (reminder.CompletedOccurrenceKeys.Length > PersonalReminderCatalog.CompletedKeysMaxLength)
            throw new ArgumentException("عدد المواعيد المنجزة أكبر من الحد — أزل بعض المواعيد أو أنشئ تذكيرًا جديدًا");

        _reminders.Update(reminder);
        await _uow.SaveChangesAsync(ct);
        return PersonalReminderService.ToDto(reminder);
    }
}
