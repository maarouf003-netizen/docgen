using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Reminders;

/// <summary>تذكيرات المحامي الشخصية — المالك فقط (`includeArchived` للأرشيف).</summary>
public sealed record ListPersonalRemindersQuery(
    int UserId,
    bool IncludeArchived = false) : IQuery<List<PersonalReminderDto>>;

internal sealed class ListPersonalRemindersQueryHandler
    : QueryHandlerBase<ListPersonalRemindersQuery, List<PersonalReminderDto>>
{
    private readonly IPersonalReminderRepository _reminders;

    public ListPersonalRemindersQueryHandler(
        IPersonalReminderRepository reminders,
        IEnumerable<IPipelineBehavior<ListPersonalRemindersQuery, List<PersonalReminderDto>>> behaviors)
        : base(behaviors)
        => _reminders = reminders;

    protected override async Task<List<PersonalReminderDto>> HandleCoreAsync(
        ListPersonalRemindersQuery query, CancellationToken ct)
    {
        var reminders = await _reminders.ListByLawyerAsync(query.UserId, query.IncludeArchived, ct);
        return reminders.Select(PersonalReminderService.ToDto).ToList();
    }
}
