using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Portal;

/// <summary>نطاق المندوب (هوية/فرع) — null إن لم يُربط بنطاق بعد.</summary>
public sealed record GetPortalScopeQuery(int UserId) : IQuery<PortalScopeDto?>;

internal sealed class GetPortalScopeQueryHandler
    : QueryHandlerBase<GetPortalScopeQuery, PortalScopeDto?>
{
    private readonly IPortalRepository _portal;

    public GetPortalScopeQueryHandler(
        IPortalRepository portal,
        IEnumerable<IPipelineBehavior<GetPortalScopeQuery, PortalScopeDto?>> behaviors)
        : base(behaviors)
        => _portal = portal;

    protected override async Task<PortalScopeDto?> HandleCoreAsync(
        GetPortalScopeQuery query, CancellationToken ct)
    {
        var scope = await _portal.ResolveForUserAsync(query.UserId, ct);
        if (scope is null || scope.GroupId == 0)
            return scope is null ? null : new PortalScopeDto("group", 0, string.Empty, PublicEntityTypeCatalog.Ministry,
                Array.Empty<PortalScopeEntryDto>());

        return new PortalScopeDto(scope.ScopeType, scope.GroupId, scope.CanonicalName, scope.EntityType,
            scope.Entries.Select(e => new PortalScopeEntryDto(e.Id, e.Governorate, e.BranchName, e.IsActive)).ToList());
    }
}
