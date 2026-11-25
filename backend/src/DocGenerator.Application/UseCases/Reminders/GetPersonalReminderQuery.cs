using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Reminders;

/// <summary>
/// جلب مباشر بالمعرف مع فحص الملكية — غير المالك يُعامل كغياب (`null`)
/// دون كشف الوجود.
/// </summary>
public sealed record GetPersonalReminderQuery(
    int Id,
    int UserId) : IQuery<PersonalReminderDto?>;

internal sealed class GetPersonalReminderQueryHandler
    : QueryHandlerBase<GetPersonalReminderQuery, PersonalReminderDto?>
{
    private readonly IPersonalReminderRepository _reminders;

    public GetPersonalReminderQueryHandler(
        IPersonalReminderRepository reminders,
        IEnumerable<IPipelineBehavior<GetPersonalReminderQuery, PersonalReminderDto?>> behaviors)
        : base(behaviors)
        => _reminders = reminders;

    protected override async Task<PersonalReminderDto?> HandleCoreAsync(
        GetPersonalReminderQuery query, CancellationToken ct)
    {
        var reminder = await _reminders.GetByIdAsync(query.Id, ct);
        if (reminder is null || reminder.LawyerId != query.UserId)
            return null;
        return PersonalReminderService.ToDto(reminder);
    }
}
