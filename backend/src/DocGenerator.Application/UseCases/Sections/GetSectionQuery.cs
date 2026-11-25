using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Sections;

/// <summary>شعبة واحدة بالمعرّف — `null` يعني غير موجودة (يُترجم 404).</summary>
public sealed record GetSectionQuery(int SectionId) : IQuery<SectionDto?>;

internal sealed class GetSectionQueryHandler : QueryHandlerBase<GetSectionQuery, SectionDto?>
{
    private readonly IRepository<Section> _sections;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IUserRepository _users;
    private readonly IReferenceDataCache? _cache;

    public GetSectionQueryHandler(
        IRepository<Section> sections,
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IUserRepository users,
        IEnumerable<IPipelineBehavior<GetSectionQuery, SectionDto?>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _sections = sections;
        _branches = branches;
        _circuits = circuits;
        _users = users;
        _cache = cache;
    }

    protected override async Task<SectionDto?> HandleCoreAsync(
        GetSectionQuery query, CancellationToken ct)
    {
        var section = await _sections.GetByIdAsync(query.SectionId, ct);
        return section is null ? null : await ToDtoAsync(section, ct);
    }

    private async Task<SectionDto> ToDtoAsync(Section section, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(section.BranchId, ct);
        return SectionProjection.ToDto(
            section,
            branch?.Name,
            await SectionProjection.ListCircuitsAsync(_circuits, _cache, ct),
            await _users.ListUsersBySectionAsync(section.Id, ct));
    }
}
