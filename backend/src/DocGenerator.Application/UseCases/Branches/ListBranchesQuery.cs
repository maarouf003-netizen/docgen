using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Branches;

/// <summary>قائمة الفروع — الأسماء/الترتيب مرجعي؛ عدّادات المستخدمين/المستندات حيّة.</summary>
public sealed record ListBranchesQuery : IQuery<List<BranchDto>>;

internal sealed class ListBranchesQueryHandler : QueryHandlerBase<ListBranchesQuery, List<BranchDto>>
{
    private readonly IBranchRepository _branches;
    private readonly IReferenceDataCache? _cache;

    public ListBranchesQueryHandler(
        IBranchRepository branches,
        IEnumerable<IPipelineBehavior<ListBranchesQuery, List<BranchDto>>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _branches = branches;
        _cache = cache;
    }

    // الأسماء/الترتيب مرجعي؛ عدّادات المستخدمين/المستندات تُحسب حيّة بعد القراءة.
    private Task<List<Branch>> ListBranchesTrackedAsync(CancellationToken ct)
        => _cache is null
            ? _branches.ListAsync(ct)
            : _cache.GetOrCreateAsync(ReferenceDataGroups.Branches, "all", _branches.ListNoTrackingAsync, ct);

    protected override async Task<List<BranchDto>> HandleCoreAsync(
        ListBranchesQuery query, CancellationToken ct)
    {
        var branches = await ListBranchesTrackedAsync(ct);
        var userCounts = await _branches.CountUsersByBranchAsync(ct);
        var documentCounts = await _branches.CountDocumentsByBranchAsync(ct);

        return branches
            .OrderBy(b => b.Name)
            .Select(b => BranchProjection.ToDto(
                b,
                userCounts.GetValueOrDefault(b.Id),
                documentCounts.GetValueOrDefault(b.Id)))
            .ToList();
    }
}
