using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Alerts;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري لمعالجات قراءات التنبيهات (`P4-01cw`): يحافظ على توقيعات
/// خدمة التنبيهات القديمة فوق سجل المعالجات — منطق الاختبارات كما هو.
/// </summary>
public static class HeadAlertReadQueryExtensions
{
    public static Task<PagedResult<HeadAlertSummaryDto>> ListForActorAsync(
        this HeadAlertReadHandlers handlers,
        HeadAlertActor actor,
        HeadAlertListQuery query,
        CancellationToken ct = default)
        => handlers.ListForActor.HandleAsync(new ListHeadAlertsForActorQuery(actor, query), ct);

    public static Task<PagedResult<HeadAlertSummaryDto>> ListSentAsync(
        this HeadAlertReadHandlers handlers,
        HeadAlertActor actor,
        AlertArchiveFilter archive,
        AlertPageRequest page,
        string? cursor,
        CancellationToken ct = default)
        => handlers.Sent.HandleAsync(new ListSentHeadAlertsQuery(actor, archive, page, cursor), ct);

    public static Task<PagedResult<HeadAlertSummaryDto>> ListByDelegationAsync(
        this HeadAlertReadHandlers handlers,
        int delegationId,
        HeadAlertActor actor,
        AlertPageRequest page,
        CancellationToken ct = default)
        => handlers.ByDelegation.HandleAsync(
            new ListHeadAlertsByDelegationQuery(delegationId, actor, page), ct);

    public static Task<HeadAlertDto?> GetDetailsAsync(
        this HeadAlertReadHandlers handlers,
        int id,
        HeadAlertActor actor,
        CancellationToken ct = default)
        => handlers.Details.HandleAsync(new GetHeadAlertDetailsQuery(id, actor), ct);

    public static Task<List<HeadAlertRecipientDto>?> GetRecipientsAsync(
        this HeadAlertReadHandlers handlers,
        int id,
        HeadAlertActor actor,
        CancellationToken ct = default)
        => handlers.Recipients.HandleAsync(new GetHeadAlertRecipientsQuery(id, actor), ct);

    public static Task<int> CountUnreadAsync(
        this HeadAlertReadHandlers handlers,
        HeadAlertActor actor,
        AlertUnreadScope scope,
        CancellationToken ct = default)
        => handlers.Unread.HandleAsync(new CountUnreadHeadAlertsQuery(actor, scope), ct);
}
