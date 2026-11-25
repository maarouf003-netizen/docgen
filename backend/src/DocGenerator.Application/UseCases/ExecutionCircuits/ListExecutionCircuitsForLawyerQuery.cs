using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// قوائم اختيار الدوائر للمحامين (كل دوائر الفرع) مقابل الرؤساء (دوائر نطاقهم فقط) —
/// `LimitToOwner` يميّز الحالتين صراحةً بلا قيمة سحرية.
/// </summary>
public sealed record ListExecutionCircuitsForLawyerQuery(
    int BranchId, int? OwnerSectionId, bool LimitToOwner) : IQuery<List<ExecutionCircuitDto>>;

internal sealed class ListExecutionCircuitsForLawyerQueryHandler
    : QueryHandlerBase<ListExecutionCircuitsForLawyerQuery, List<ExecutionCircuitDto>>
{
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<Section> _sections;
    private readonly IReferenceDataCache? _cache;

    public ListExecutionCircuitsForLawyerQueryHandler(
        IRepository<ExecutionCircuit> circuits,
        IRepository<Section> sections,
        IEnumerable<IPipelineBehavior<ListExecutionCircuitsForLawyerQuery, List<ExecutionCircuitDto>>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _circuits = circuits;
        _sections = sections;
        _cache = cache;
    }

    protected override async Task<List<ExecutionCircuitDto>> HandleCoreAsync(
        ListExecutionCircuitsForLawyerQuery query, CancellationToken ct)
    {
        var all = await ReferenceDataQueries.ListCircuitsAsync(_circuits, _cache, ct);
        var sectionNames = await ExecutionCircuitProjection.SectionNamesAsync(_sections, _cache, ct);
        return all.Where(c => c.BranchId == query.BranchId && c.IsActive
                && (!query.LimitToOwner || c.SectionId == query.OwnerSectionId))
            .OrderBy(c => c.Name)
            .Select(c => new ExecutionCircuitDto(
                c.Id, c.BranchId, null, c.Name, c.IsActive, 0, 0, c.Version,
                c.SectionId, c.SectionId.HasValue ? sectionNames.GetValueOrDefault(c.SectionId.Value) : null)).ToList();
    }
}
