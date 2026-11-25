using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Alerts;

/// <summary>
/// قائمة المستلَم حسب الدور (`P4-01cw`): المحامي استلامه الشخصي بلا قيد فرع،
/// ورئيس القسم/الشعبة ما استلمه في فرعه، والمدير/المشرف إشراف كل الفروع.
/// </summary>
public sealed record ListHeadAlertsForActorQuery(
    HeadAlertActor Actor,
    HeadAlertListQuery Filter) : IQuery<PagedResult<HeadAlertSummaryDto>>;

internal sealed class ListHeadAlertsForActorQueryHandler
    : QueryHandlerBase<ListHeadAlertsForActorQuery, PagedResult<HeadAlertSummaryDto>>
{
    private readonly IHeadAlertRepository _alerts;

    public ListHeadAlertsForActorQueryHandler(
        IHeadAlertRepository alerts,
        IEnumerable<IPipelineBehavior<ListHeadAlertsForActorQuery, PagedResult<HeadAlertSummaryDto>>> behaviors)
        : base(behaviors)
        => _alerts = alerts;

    protected override async Task<PagedResult<HeadAlertSummaryDto>> HandleCoreAsync(
        ListHeadAlertsForActorQuery request, CancellationToken ct)
    {
        var actor = request.Actor;
        var query = request.Filter;

        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(query);

        // التفويض قبل التحقق من الشكل (عقد ما قبل توحيد الصلاحيات): دور غير مخوّل أو
        // رئيس بلا فرع يُرفض 403 حتى مع ترقيم غير صالح، و«غير المقروء» للمدير/المشرف
        // 400 برسالته الخاصة قبل أي تحقق آخر — لا انعكاس 403/400 غير مقصود.
        if (!actor.IsManagerOrAdmin && !actor.IsLawyer && !actor.IsHeadOrSubHead)
            throw new UnauthorizedAccessException("الدور غير مخوّل لقوائم التنبيهات");

        if (actor.IsManagerOrAdmin && query.UnreadOnly)
            throw new ArgumentException("خيار «غير المقروء» غير متاح للمدير/المشرف");

        var branchId = actor.IsHeadOrSubHead ? HeadAlertReadHelpers.RequireBranch(actor) : (int?)null;

        query.Page.Validate();
        // شكل المؤشر يُفحص بعد الدور/الفرع أيضًا (403 قبل 400)، ويُحلَّل مرة واحدة.
        var cursor = AlertCursor.ParseOptional(query.Cursor);

        if (actor.IsManagerOrAdmin)
        {
            var page = await _alerts.ListSummariesForOversightAsync(
                query.Archive,
                query.DelegationId,
                cursor,
                query.Page.Skip,
                query.Page.PageSize,
                ct);
            return HeadAlertReadHelpers.ToPagedResult(page, query.Page, cursor);
        }

        if (actor.IsLawyer)
        {
            var page = await _alerts.ListSummariesForRecipientAsync(
                actor.UserId,
                branchId: null,
                query.Archive,
                query.UnreadOnly,
                query.DelegationId,
                cursor,
                query.Page.Skip,
                query.Page.PageSize,
                includeCounters: false,
                ct);
            return HeadAlertReadHelpers.ToPagedResult(page, query.Page, cursor);
        }

        var headPage = await _alerts.ListSummariesForRecipientAsync(
            actor.UserId,
            branchId,
            query.Archive,
            query.UnreadOnly,
            query.DelegationId,
            cursor,
            query.Page.Skip,
            query.Page.PageSize,
            includeCounters: true,
            ct);
        return HeadAlertReadHelpers.ToPagedResult(headPage, query.Page, cursor);
    }
}
