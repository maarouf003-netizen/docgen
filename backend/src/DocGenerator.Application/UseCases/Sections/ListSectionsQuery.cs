using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Sections;

/// <summary>قائمة شعب فرع — الفرع إلزامي صراحةً (يتحقق منه المتحكم).</summary>
public sealed record ListSectionsQuery(int BranchId) : IQuery<List<SectionDto>>;

internal sealed class ListSectionsQueryHandler : QueryHandlerBase<ListSectionsQuery, List<SectionDto>>
{
    private readonly IRepository<Section> _sections;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IUserRepository _users;
    private readonly IReferenceDataCache? _cache;

    public ListSectionsQueryHandler(
        IRepository<Section> sections,
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IUserRepository users,
        IEnumerable<IPipelineBehavior<ListSectionsQuery, List<SectionDto>>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _sections = sections;
        _branches = branches;
        _circuits = circuits;
        _users = users;
        _cache = cache;
    }

    protected override async Task<List<SectionDto>> HandleCoreAsync(
        ListSectionsQuery query, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(query.BranchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        var all = await SectionProjection.ListSectionsAsync(_sections, _cache, ct);
        var sections = all.Where(s => s.BranchId == query.BranchId).OrderBy(s => s.Name).ToList();
        if (sections.Count == 0)
            return new List<SectionDto>();

        var circuits = await SectionProjection.ListCircuitsAsync(_circuits, _cache, ct);
        var circuitCounts = circuits
            .Where(c => c.SectionId.HasValue)
            .GroupBy(c => c.SectionId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var allSectionUsers = new List<User>();
        foreach (var s in sections)
            allSectionUsers.AddRange(await _users.ListUsersBySectionAsync(s.Id, ct));
        var headNames = allSectionUsers
            .Where(u => u.IsActive && u.Role == UserRole.SubHead)
            .GroupBy(u => u.SectionId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(u => u.Id).First().FullName);

        return sections.Select(s => new SectionDto(
            s.Id,
            s.BranchId,
            branch.Name,
            s.Name,
            s.IsActive,
            circuitCounts.GetValueOrDefault(s.Id),
            headNames.GetValueOrDefault(s.Id),
            s.Version)).ToList();
    }
}
