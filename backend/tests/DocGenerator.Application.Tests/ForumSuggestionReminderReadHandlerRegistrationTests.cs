using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Forum;
using DocGenerator.Application.UseCases.Reminders;
using DocGenerator.Application.UseCases.Suggestions;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Application.Tests;

/// <summary>
/// توصيل استعلامات المنتدى واقتراحات التطبيق والتذكيرات الشخصية (`P4-01cu`) في حاوية
/// التطبيق: كل استعلام مسجَّل مرة واحدة بالنوع الدقيق، ولا خدمة قراءة قديمة مسجَّلة —
/// يحمي خط الأنابيب بعد حذف الواجهات الثلاث.
/// </summary>
public class ForumSuggestionReminderReadHandlerRegistrationTests
{
    [Fact]
    public void AddApplication_RegistersEveryMovedQueryHandlerOnce_AndRetiresReadServices()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var expected = new[]
        {
            typeof(IQueryHandler<ListForumMessagesQuery, ForumMessagesPageDto>),
            typeof(IQueryHandler<GetForumMessageQuery, ForumMessageDto?>),
            typeof(IQueryHandler<GetPinnedForumMessageQuery, ForumMessageDto?>),
            typeof(IQueryHandler<GetForumMessageReadersQuery, List<ForumReaderDto>>),
            typeof(IQueryHandler<SearchForumUsersQuery, List<ForumUserLookupDto>>),
            typeof(IQueryHandler<CountUnreadForumMessagesQuery, int>),
            typeof(IQueryHandler<ListMyAppSuggestionsQuery, List<AppSuggestionDto>>),
            typeof(IQueryHandler<GetAppSuggestionQuery, AppSuggestionDto?>),
            typeof(IQueryHandler<ListAppSuggestionsForAdminQuery, PagedResult<AppSuggestionDto>>),
            typeof(IQueryHandler<CountUnreadAppSuggestionsQuery, int>),
            typeof(IQueryHandler<ListPersonalRemindersQuery, List<PersonalReminderDto>>),
            typeof(IQueryHandler<GetPersonalReminderQuery, PersonalReminderDto?>),
        };

        foreach (var contract in expected)
        {
            var matches = services.Where(d => d.ServiceType == contract).ToList();
            Assert.Single(matches);
        }

        Assert.DoesNotContain(services, d =>
            d.ServiceType.Name is "IForumService" or "IAppSuggestionService" or "IPersonalReminderService");
    }
}
