using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// كل ملفات الدائرة (غير المحذوفة) أيًا كانت حالتها — نافذة الإفراغ: المخفية عن
/// فلتر البحث العام («منفذ إنابة» وغيرها) شاملة، بلا حدّ صفحات. نطاق المالك إلزامي.
/// </summary>
public sealed record ListCircuitFilesQuery(int CircuitId, int BranchId, int? OwnerSectionId)
    : IQuery<List<CircuitFileDto>>;

internal sealed class ListCircuitFilesQueryHandler
    : QueryHandlerBase<ListCircuitFilesQuery, List<CircuitFileDto>>
{
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IDocumentRepository _documents;

    public ListCircuitFilesQueryHandler(
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IDocumentRepository documents,
        IEnumerable<IPipelineBehavior<ListCircuitFilesQuery, List<CircuitFileDto>>> behaviors)
        : base(behaviors)
    {
        _branches = branches;
        _circuits = circuits;
        _documents = documents;
    }

    protected override async Task<List<CircuitFileDto>> HandleCoreAsync(
        ListCircuitFilesQuery query, CancellationToken ct)
    {
        await ExecutionCircuitProjection.RequireBranchWithGovernorateAsync(_branches, query.BranchId, ct);
        _ = await ExecutionCircuitRules.GetOwnedAsync(
            query.CircuitId, query.BranchId, query.OwnerSectionId, _circuits, ct);
        var docs = await _documents.ListCircuitFileSummariesAsync(query.CircuitId, ct);
        return docs.Select(d => new CircuitFileDto(
            d.Id, d.BorrowerName, d.FileNumber, d.FileType, d.FileYear, d.Lawyer)).ToList();
    }
}
