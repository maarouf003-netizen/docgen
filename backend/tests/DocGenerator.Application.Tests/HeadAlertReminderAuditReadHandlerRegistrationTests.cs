using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Alerts;
using DocGenerator.Application.UseCases.Audit;
using DocGenerator.Application.UseCases.Reminders;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Application.Tests;

/// <summary>
/// توصيل استعلامات التنبيهات وتذكيرات الشطب وسجل التدقيق (`P4-01cw`) في حاوية
/// التطبيق: كل استعلام مسجَّل مرة واحدة بالنوع الدقيق، ولا خدمة قراءة قديمة مسجَّلة —
/// مع بقاء <see cref="IHeadAlertService"/> لسطح الكتابة النظامية عمدًا.
/// </summary>
public class HeadAlertReminderAuditReadHandlerRegistrationTests
{
    [Fact]
    public void AddApplication_RegistersEveryMovedQueryHandlerOnce_AndRetiresReadServices()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var expected = new[]
        {
            typeof(IQueryHandler<ListHeadAlertsForActorQuery, PagedResult<HeadAlertSummaryDto>>),
            typeof(IQueryHandler<ListSentHeadAlertsQuery, PagedResult<HeadAlertSummaryDto>>),
            typeof(IQueryHandler<ListHeadAlertsByDelegationQuery, PagedResult<HeadAlertSummaryDto>>),
            typeof(IQueryHandler<GetHeadAlertDetailsQuery, HeadAlertDto?>),
            typeof(IQueryHandler<GetHeadAlertRecipientsQuery, List<HeadAlertRecipientDto>?>),
            typeof(IQueryHandler<CountUnreadHeadAlertsQuery, int>),
            typeof(IQueryHandler<GetStrikeOffRemindersQuery, PagedResult<DocumentResponse>>),
            typeof(IQueryHandler<GetStrikeOffReminderListQuery, PagedResult<DocumentListItemDto>>),
            typeof(IQueryHandler<CountStrikeOffRemindersQuery, int>),
            typeof(IQueryHandler<SearchAuditLogsQuery, PagedResult<AuditLogDto>>),
            typeof(IQueryHandler<GetDocumentChangesQuery, PagedResult<DocumentChangeGroupDto>>),
        };

        foreach (var contract in expected)
        {
            var matches = services.Where(d => d.ServiceType == contract).ToList();
            Assert.Single(matches);
        }

        Assert.DoesNotContain(services, d =>
            d.ServiceType.Name is "IStrikeOffReminderService" or "IAuditLogService");

        var alertWriteSurface = services.Where(d => d.ServiceType == typeof(IHeadAlertService)).ToList();
        Assert.Single(alertWriteSurface);
    }
}
