using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// صفوف إحصاءات الدوائر بالنطاق (قرار §2.27): الرئيس لدوائر نطاقه فقط،
/// والمدير/المشرف للكل — زائد صف اصطناعي واحد «بلا دائرة» لملفات النطاق بلا دائرة
/// عندما وُجدت. العدّادات حيّة خارج الكاش دائمًا.
/// </summary>
public sealed record GetCircuitStatsQuery(int? BranchId, int? OwnerSectionId, bool FullAccess)
    : IQuery<List<CircuitStatsDto>>;

internal sealed class GetCircuitStatsQueryHandler
    : QueryHandlerBase<GetCircuitStatsQuery, List<CircuitStatsDto>>
{
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Section> _sections;
    private readonly IDocumentRepository _documents;
    private readonly IReferenceDataCache? _cache;

    public GetCircuitStatsQueryHandler(
        IRepository<ExecutionCircuit> circuits,
        IRepository<Branch> branches,
        IRepository<Section> sections,
        IDocumentRepository documents,
        IEnumerable<IPipelineBehavior<GetCircuitStatsQuery, List<CircuitStatsDto>>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _circuits = circuits;
        _branches = branches;
        _sections = sections;
        _documents = documents;
        _cache = cache;
    }

    protected override async Task<List<CircuitStatsDto>> HandleCoreAsync(
        GetCircuitStatsQuery query, CancellationToken ct)
    {
        var all = await ReferenceDataQueries.ListCircuitsAsync(_circuits, _cache, ct);
        var circuits = (query.BranchId is null
            ? all
            : all.Where(c => c.BranchId == query.BranchId.Value));
        if (!query.FullAccess)
            circuits = circuits.Where(c => c.SectionId == query.OwnerSectionId);
        var ordered = circuits.OrderBy(c => c.BranchId).ThenBy(c => c.Name).ToList();
        var branches = await ReferenceDataQueries.ListBranchesAsync(_branches, _cache, ct);
        var branchNames = branches.ToDictionary(b => b.Id, b => b.Name);
        // أسماء الشعب دفعة واحدة — بلا استعلام لكل دائرة (N+1).
        var sectionNames = (await ReferenceDataQueries.ListSectionsAsync(_sections, _cache, ct))
            .Where(s => ordered.Any(c => c.SectionId == s.Id))
            .ToDictionary(s => s.Id, s => s.Name);
        var counts = await _documents.CountByCircuitsAsync(ordered.Select(c => c.Id).ToList(), ct);
        // المحامون النشطون (≥ ملف واحد): استعلام تجميعي واحد — بلا تحميل كيانات (N+1).
        var lawyerCounts = await _documents.CountLawyersByCircuitsAsync(ordered.Select(c => c.Id).ToList(), ct);
        var rows = ordered.Select(c =>
        {
            counts.TryGetValue(c.Id, out var v);
            return new CircuitStatsDto(c.Id, c.Name, c.BranchId,
                branchNames.TryGetValue(c.BranchId, out var n) ? n : null,
                c.IsActive, v.FileCount, lawyerCounts.TryGetValue(c.Id, out var lc) ? lc : 0, v.PendingCount,
                c.SectionId,
                c.SectionId.HasValue && sectionNames.TryGetValue(c.SectionId.Value, out var s) ? s : null,
                c.Version);
        }).ToList();
        if (!query.OwnerSectionId.HasValue)
        {
            var (fileCount, pendingCount, lawyerCount) = await _documents.CountWithoutCircuitAsync(query.BranchId, ct);
            if (fileCount > 0)
            {
                branchNames.TryGetValue(query.BranchId ?? 0, out var branchName);
                rows.Add(new CircuitStatsDto(0, "بلا دائرة", query.BranchId ?? 0, branchName,
                    true, fileCount, lawyerCount, pendingCount, null, null, 0));
            }
        }
        return rows;
    }
}
