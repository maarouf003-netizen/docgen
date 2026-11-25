using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Auth;
using DocGenerator.Application.UseCases.Branches;
using DocGenerator.Application.UseCases.Sections;
using DocGenerator.Application.UseCases.Users;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Application.Tests;

/// <summary>
/// توصيل استعلامات الهيكل التنظيمي (`P4-01cs`) في حاوية التطبيق: كل استعلام
/// مسجَّل مرة واحدة بالنوع الدقيق — يحمي خط الأنابيب بعد حذف الخدمات القديمة.
/// </summary>
public class OrgStructureReadHandlerRegistrationTests
{
    [Fact]
    public void AddApplication_RegistersEveryOrgStructureQueryHandlerOnce()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var expected = new[]
        {
            typeof(IQueryHandler<GetUserQuery, UserDto?>),
            typeof(IQueryHandler<ListLawyersQuery, List<LawyerListItemDto>>),
            typeof(IQueryHandler<ListUsersQuery, List<UserListItemDto>>),
            typeof(IQueryHandler<ListSuccessionQuery, List<HeadSuccessionDto>>),
            typeof(IQueryHandler<ListBranchesQuery, List<BranchDto>>),
            typeof(IQueryHandler<ListSectionsQuery, List<SectionDto>>),
            typeof(IQueryHandler<GetSectionQuery, SectionDto?>),
        };

        foreach (var contract in expected)
        {
            var matches = services.Where(d => d.ServiceType == contract).ToList();
            Assert.Single(matches);
        }
    }
}
