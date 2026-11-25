using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Portal;

/// <summary>بطاقة الاستئنافات القرائية لملف داخل النطاق — null إذا خرج عن النطاق.</summary>
public sealed record ListPortalAppealsQuery(int UserId, int DocumentId) : IQuery<IReadOnlyList<PortalAppealDto>?>;

internal sealed class ListPortalAppealsQueryHandler
    : QueryHandlerBase<ListPortalAppealsQuery, IReadOnlyList<PortalAppealDto>?>
{
    private readonly IPortalRepository _portal;
    private readonly IAppealRepository _appeals;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ListPortalAppealsQueryHandler(
        IPortalRepository portal,
        IAppealRepository appeals,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<ListPortalAppealsQuery, IReadOnlyList<PortalAppealDto>?>> behaviors)
        : base(behaviors)
    {
        _portal = portal;
        _appeals = appeals;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<IReadOnlyList<PortalAppealDto>?> HandleCoreAsync(
        ListPortalAppealsQuery query, CancellationToken ct)
    {
        if (!await PortalService.IsInScopeAsync(_portal, query.UserId, query.DocumentId, ct))
            return null;

        var appeals = await _appeals.ListByDocumentAsync(query.DocumentId, ct);
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return appeals.Select(a =>
        {
            // البوابة تعرض الهوية الفعّالة (رقم السنة الحالية أو آخر رقم مسجّل) لا القيد الأصلي.
            var identity = AppealBaseNumberPolicy.ResolveCurrent(a, currentYear);
            return new PortalAppealDto(
                a.Id,
                a.Direction,
                a.Status,
                a.AppealTypeLabel,
                identity?.Number,
                identity?.Year?.ToString(CultureInfo.InvariantCulture),
                a.CreatedAt,
                a.DecisionDate,
                a.DecisionRuling);
        }).ToList();
    }
}
