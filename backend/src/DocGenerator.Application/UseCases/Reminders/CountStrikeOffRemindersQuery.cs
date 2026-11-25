using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;

namespace DocGenerator.Application.UseCases.Reminders;

/// <summary>عدد ملفات «تذكيرات الشطب» — لشارة بطاقة لوحة المحامي.</summary>
public sealed record CountStrikeOffRemindersQuery(
    int? VisibleBranchId,
    int? VisibleUserId,
    int? OwnerSectionId) : IQuery<int>;

internal sealed class CountStrikeOffRemindersQueryHandler
    : QueryHandlerBase<CountStrikeOffRemindersQuery, int>
{
    private readonly IDocumentRepository _documents;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public CountStrikeOffRemindersQueryHandler(
        IDocumentRepository documents,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<CountStrikeOffRemindersQuery, int>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<int> HandleCoreAsync(
        CountStrikeOffRemindersQuery request, CancellationToken ct)
    {
        var visibleBranchId = request.VisibleBranchId;
        var visibleUserId = request.VisibleUserId;
        var ownerSectionId = request.OwnerSectionId;

        var cutoff = StrikeOffReminderCalculator.CutoffUtc(ServerClock.Now(_clock, _timeZone), _timeZone);
        var (total, _, _) = await _documents.SearchStrikeOffReminderIdsAsync(
            cutoff, visibleBranchId, visibleUserId, 1, 1, ct, ownerSectionId);
        return total;
    }
}
