using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// دوائر فرع محافظة محددة النشطة — لاختيار الدائرة عند الإنابة. المحافظة غير
/// المعتمدة تُرجع قائمة فارغة (لا استثناء) كما الأصل.
/// </summary>
public sealed record ListExecutionCircuitsForDelegationQuery(string? Governorate)
    : IQuery<List<ExecutionCircuitDto>>;

internal sealed class ListExecutionCircuitsForDelegationQueryHandler
    : QueryHandlerBase<ListExecutionCircuitsForDelegationQuery, List<ExecutionCircuitDto>>
{
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IReferenceDataCache? _cache;

    public ListExecutionCircuitsForDelegationQueryHandler(
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IEnumerable<IPipelineBehavior<ListExecutionCircuitsForDelegationQuery, List<ExecutionCircuitDto>>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _branches = branches;
        _circuits = circuits;
        _cache = cache;
    }

    protected override async Task<List<ExecutionCircuitDto>> HandleCoreAsync(
        ListExecutionCircuitsForDelegationQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Governorate)
            || !GovernorateCatalog.IsGovernorate(query.Governorate!.Trim()))
            return new List<ExecutionCircuitDto>();
        var gov = query.Governorate!.Trim();
        var branches = await ReferenceDataQueries.ListBranchesAsync(_branches, _cache, ct);
        var branchIds = branches.Where(b => b.Governorate == gov).Select(b => b.Id).ToHashSet();
        if (branchIds.Count == 0) return new List<ExecutionCircuitDto>();
        var all = await ReferenceDataQueries.ListCircuitsAsync(_circuits, _cache, ct);
        return all.Where(c => branchIds.Contains(c.BranchId) && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ExecutionCircuitDto(
                c.Id, c.BranchId, null, c.Name, c.IsActive, 0, 0, c.Version)).ToList();
    }
}
