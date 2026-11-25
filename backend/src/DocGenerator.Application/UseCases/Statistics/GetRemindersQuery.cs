using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Statistics;

/// <summary>تذكيرات إجراءات التنفيذ الخاصة بمستخدم محدد.</summary>
public sealed record GetRemindersQuery(int UserId) : IQuery<List<ReminderDto>>;

internal sealed class GetRemindersQueryHandler
    : QueryHandlerBase<GetRemindersQuery, List<ReminderDto>>
{
    private readonly IStatisticsRepository _stats;

    public GetRemindersQueryHandler(
        IStatisticsRepository stats,
        IEnumerable<IPipelineBehavior<GetRemindersQuery, List<ReminderDto>>> behaviors)
        : base(behaviors)
        => _stats = stats;

    protected override Task<List<ReminderDto>> HandleCoreAsync(
        GetRemindersQuery query, CancellationToken ct)
        => _stats.GetRemindersAsync(query.UserId, ct);
}
