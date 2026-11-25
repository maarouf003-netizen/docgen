using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Delegations;

namespace DocGenerator.Application.UseCases.Portal;

/// <summary>تشعبات الملف القرائية (إنابة) — null خارج النطاق.</summary>
public sealed record ListPortalDelegationsQuery(int UserId, int DocumentId)
    : IQuery<IReadOnlyList<DelegationDto>?>;

internal sealed class ListPortalDelegationsQueryHandler
    : QueryHandlerBase<ListPortalDelegationsQuery, IReadOnlyList<DelegationDto>?>
{
    private readonly IPortalRepository _portal;
    private readonly IDocumentRepository _documents;
    private readonly IQueryHandler<GetDocumentDelegationsQuery, List<DelegationDto>> _delegationReads;

    public ListPortalDelegationsQueryHandler(
        IPortalRepository portal,
        IDocumentRepository documents,
        IQueryHandler<GetDocumentDelegationsQuery, List<DelegationDto>> delegationReads,
        IEnumerable<IPipelineBehavior<ListPortalDelegationsQuery, IReadOnlyList<DelegationDto>?>> behaviors)
        : base(behaviors)
    {
        _portal = portal;
        _documents = documents;
        _delegationReads = delegationReads;
    }

    protected override async Task<IReadOnlyList<DelegationDto>?> HandleCoreAsync(
        ListPortalDelegationsQuery query, CancellationToken ct)
    {
        var doc = await PortalService.GetScopedDocumentAsync(_portal, _documents, query.UserId, query.DocumentId, ct);
        if (doc is null)
            return null;

        return await _delegationReads.HandleAsync(new GetDocumentDelegationsQuery(query.DocumentId), ct);
    }
}
