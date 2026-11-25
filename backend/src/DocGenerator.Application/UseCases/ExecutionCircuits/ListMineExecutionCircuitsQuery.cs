using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// دوائر نطاق المالك (القسم أو الشعبة) مع عدّادات الملفات الحيّة — قراءة صرفة:
/// بوابة فرع المحافظة أولًا، ثم نطاق المالك، ثم العدّادات خارج الكاش.
/// </summary>
public sealed record ListMineExecutionCircuitsQuery(int BranchId, int? OwnerSectionId)
    : IQuery<List<ExecutionCircuitDto>>;

internal sealed class ListMineExecutionCircuitsQueryHandler
    : QueryHandlerBase<ListMineExecutionCircuitsQuery, List<ExecutionCircuitDto>>
{
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IDocumentRepository _documents;
    private readonly IRepository<Section> _sections;
    private readonly IReferenceDataCache? _cache;

    public ListMineExecutionCircuitsQueryHandler(
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IDocumentRepository documents,
        IRepository<Section> sections,
        IEnumerable<IPipelineBehavior<ListMineExecutionCircuitsQuery, List<ExecutionCircuitDto>>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _branches = branches;
        _circuits = circuits;
        _documents = documents;
        _sections = sections;
        _cache = cache;
    }

    protected override async Task<List<ExecutionCircuitDto>> HandleCoreAsync(
        ListMineExecutionCircuitsQuery query, CancellationToken ct)
    {
        await ExecutionCircuitProjection.RequireBranchWithGovernorateAsync(_branches, query.BranchId, ct);
        var all = await ReferenceDataQueries.ListCircuitsAsync(_circuits, _cache, ct);
        var circuits = all
            .Where(c => c.BranchId == query.BranchId && c.SectionId == query.OwnerSectionId)
            .OrderBy(c => c.Name)
            .ToList();
        var counts = await _documents.CountByCircuitsAsync(circuits.Select(c => c.Id).ToList(), ct);
        var sectionNames = await ExecutionCircuitProjection.SectionNamesAsync(_sections, _cache, ct);
        return circuits.Select(c => new ExecutionCircuitDto(
            c.Id, c.BranchId, null, c.Name, c.IsActive,
            counts.TryGetValue(c.Id, out var v) ? v.FileCount : 0,
            counts.TryGetValue(c.Id, out var v2) ? v2.PendingCount : 0,
            c.Version, c.SectionId,
            c.SectionId.HasValue ? sectionNames.GetValueOrDefault(c.SectionId.Value) : null,
            counts.TryGetValue(c.Id, out var v3) ? v3.DeletedCount : 0)).ToList();
    }
}
