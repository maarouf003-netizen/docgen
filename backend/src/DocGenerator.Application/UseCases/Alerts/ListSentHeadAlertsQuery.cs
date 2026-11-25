using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Alerts;

/// <summary>
/// ما أصدره رئيس القسم/الشعبة في فرعه (تبويب «أرسلتها») — قراءة فقط، ومؤشر
/// الصفحة يُحلَّل بعد فحص الدور/الفرع (403 قبل 400).
/// </summary>
public sealed record ListSentHeadAlertsQuery(
    HeadAlertActor Actor,
    AlertArchiveFilter Archive,
    AlertPageRequest Page,
    string? Cursor) : IQuery<PagedResult<HeadAlertSummaryDto>>;

internal sealed class ListSentHeadAlertsQueryHandler
    : QueryHandlerBase<ListSentHeadAlertsQuery, PagedResult<HeadAlertSummaryDto>>
{
    private readonly IHeadAlertRepository _alerts;

    public ListSentHeadAlertsQueryHandler(
        IHeadAlertRepository alerts,
        IEnumerable<IPipelineBehavior<ListSentHeadAlertsQuery, PagedResult<HeadAlertSummaryDto>>> behaviors)
        : base(behaviors)
        => _alerts = alerts;

    protected override async Task<PagedResult<HeadAlertSummaryDto>> HandleCoreAsync(
        ListSentHeadAlertsQuery request, CancellationToken ct)
    {
        var actor = request.Actor;
        var archive = request.Archive;
        var page = request.Page;
        var cursor = request.Cursor;

        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(page);

        // الدور والفرع قبل التحقق من الشكل: مدير/غير رئيس ⇒ 403، ورئيس بلا فرع ⇒ 403
        // (عقد ما قبل التوحيد)، ثم الترقيم/المؤشر غير الصالح ⇒ 400.
        if (!actor.IsHeadOrSubHead)
            throw new UnauthorizedAccessException("قائمة الصادر مخصصة لرئيس القسم/الشعبة");

        var branchId = HeadAlertReadHelpers.RequireBranch(actor);

        page.Validate();
        var parsedCursor = AlertCursor.ParseOptional(cursor);

        var result = await _alerts.ListSummariesSentAsync(
            actor.UserId, branchId, archive, parsedCursor, page.Skip, page.PageSize, ct);
        return HeadAlertReadHelpers.ToPagedResult(result, page, parsedCursor);
    }
}
