using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Alerts;

/// <summary>
/// تنبيهات إنابةٍ معيّنة («معلومات الملف المنيب»/«تشعبات الملف») — نشطة فقط،
/// بعلامة قراءة بحسب المستخدم، وملخصات مرقّمة.
/// </summary>
public sealed record ListHeadAlertsByDelegationQuery(
    int DelegationId,
    HeadAlertActor Actor,
    AlertPageRequest Page) : IQuery<PagedResult<HeadAlertSummaryDto>>;

internal sealed class ListHeadAlertsByDelegationQueryHandler
    : QueryHandlerBase<ListHeadAlertsByDelegationQuery, PagedResult<HeadAlertSummaryDto>>
{
    private readonly IHeadAlertRepository _alerts;
    private readonly IDelegationRepository _delegations;

    public ListHeadAlertsByDelegationQueryHandler(
        IHeadAlertRepository alerts,
        IDelegationRepository delegations,
        IEnumerable<IPipelineBehavior<ListHeadAlertsByDelegationQuery, PagedResult<HeadAlertSummaryDto>>> behaviors)
        : base(behaviors)
    {
        _alerts = alerts;
        _delegations = delegations;
    }

    protected override async Task<PagedResult<HeadAlertSummaryDto>> HandleCoreAsync(
        ListHeadAlertsByDelegationQuery request, CancellationToken ct)
    {
        var delegationId = request.DelegationId;
        var actor = request.Actor;
        var page = request.Page;

        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(page);

        // الدور ثم الطرفية — قرار الخدمة وحده (D8/`ح‑1`): المستودع لا يقيّد النتيجة
        // بالمستخدم، فلو بقي الفحص في المتحكم لكان أي متصل آخر يقرأ تنبيهات إنابةٍ
        // لا تخصّه. الترتيب: دور (403) ← ترقيم (400) ← طرفية (403)، فالدور غير المخوّل
        // لا يتلقى 400 عن شكل طلب قبل رفض صلاحيته، ويبقى عقد التحقق من الترقيم محفوظًا.
        if (!RolePermissions.CanReadDelegationAlerts(actor.Role))
            throw new UnauthorizedAccessException("تنبيهات الإنابة متاحة لطرفها المحامي فقط");

        page.Validate();

        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct);
        if (delegation is null || !DelegationParties.IsParty(delegation, actor.UserId))
            throw new UnauthorizedAccessException("لست طرفًا في هذه الإنابة");

        var result = await _alerts.ListSummariesByDelegationAsync(
            delegationId, actor.UserId, page.Skip, page.PageSize, ct);
        return HeadAlertReadHelpers.ToPagedResult(result, page);
    }
}
