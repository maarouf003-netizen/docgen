using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Alerts;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حزمة معالجات قراءات التنبيهات (`P4-01cw`) على قاعدة اختبارية واحدة — بدل تكرار
/// ربط السلوكيات الفارغة في كل اختبار. الكتابة تبقى على <c>HeadAlertService</c>.
/// </summary>
public sealed record HeadAlertReadHandlers(
    IQueryHandler<ListHeadAlertsForActorQuery, PagedResult<HeadAlertSummaryDto>> ListForActor,
    IQueryHandler<ListSentHeadAlertsQuery, PagedResult<HeadAlertSummaryDto>> Sent,
    IQueryHandler<ListHeadAlertsByDelegationQuery, PagedResult<HeadAlertSummaryDto>> ByDelegation,
    IQueryHandler<GetHeadAlertDetailsQuery, HeadAlertDto?> Details,
    IQueryHandler<GetHeadAlertRecipientsQuery, List<HeadAlertRecipientDto>?> Recipients,
    IQueryHandler<CountUnreadHeadAlertsQuery, int> Unread);

public static class HeadAlertReadHandlerFactory
{
    public static HeadAlertReadHandlers Create(DocGeneratorDbContext db)
        => new(
            new ListHeadAlertsForActorQueryHandler(
                new HeadAlertRepository(db),
                Array.Empty<IPipelineBehavior<ListHeadAlertsForActorQuery, PagedResult<HeadAlertSummaryDto>>>()),
            new ListSentHeadAlertsQueryHandler(
                new HeadAlertRepository(db),
                Array.Empty<IPipelineBehavior<ListSentHeadAlertsQuery, PagedResult<HeadAlertSummaryDto>>>()),
            new ListHeadAlertsByDelegationQueryHandler(
                new HeadAlertRepository(db),
                new DelegationRepository(db),
                Array.Empty<IPipelineBehavior<ListHeadAlertsByDelegationQuery, PagedResult<HeadAlertSummaryDto>>>()),
            new GetHeadAlertDetailsQueryHandler(
                new HeadAlertRepository(db),
                Array.Empty<IPipelineBehavior<GetHeadAlertDetailsQuery, HeadAlertDto?>>()),
            new GetHeadAlertRecipientsQueryHandler(
                new HeadAlertRepository(db),
                Array.Empty<IPipelineBehavior<GetHeadAlertRecipientsQuery, List<HeadAlertRecipientDto>?>>()),
            new CountUnreadHeadAlertsQueryHandler(
                new HeadAlertRepository(db),
                Array.Empty<IPipelineBehavior<CountUnreadHeadAlertsQuery, int>>()));
}
