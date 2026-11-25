using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.ExecutionActions;
using DocGenerator.Application.UseCases.ExecutionCircuits;
using DocGenerator.Application.UseCases.PublicEntities;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Application.Tests;

/// <summary>
/// توصيل استعلامات الدوائر/الإجراءات/المندوبين (`P4-01ct`) في حاوية التطبيق: كل
/// استعلام مسجَّل مرة واحدة بالنوع الدقيق — يحمي خط الأنابيب بعد حذف قراءات الخدمات.
/// </summary>
public class CircuitActionDelegateReadHandlerRegistrationTests
{
    [Fact]
    public void AddApplication_RegistersEveryStageQueryHandlerOnce()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var expected = new[]
        {
            typeof(IQueryHandler<ListMineExecutionCircuitsQuery, List<ExecutionCircuitDto>>),
            typeof(IQueryHandler<ListCircuitFilesQuery, List<CircuitFileDto>>),
            typeof(IQueryHandler<ListExecutionCircuitsForLawyerQuery, List<ExecutionCircuitDto>>),
            typeof(IQueryHandler<ListExecutionCircuitsForDelegationQuery, List<ExecutionCircuitDto>>),
            typeof(IQueryHandler<ListMyPendingRegistrationsQuery, List<PendingRegistrationDto>>),
            typeof(IQueryHandler<GetCircuitStatsQuery, List<CircuitStatsDto>>),
            typeof(IQueryHandler<GetExecutionActionsQuery, List<ExecutionActionDto>>),
            typeof(IQueryHandler<ListEntityDelegatesQuery, List<DelegateDto>>),
        };

        foreach (var contract in expected)
        {
            var matches = services.Where(d => d.ServiceType == contract).ToList();
            Assert.Single(matches);
        }
    }
}
