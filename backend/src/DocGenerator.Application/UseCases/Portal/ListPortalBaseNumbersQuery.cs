using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Portal;

/// <summary>تاريخ أرقام الأساس للملف — null خارج النطاق.</summary>
public sealed record ListPortalBaseNumbersQuery(int UserId, int DocumentId)
    : IQuery<IReadOnlyList<BaseNumberHistoryDto>?>;

internal sealed class ListPortalBaseNumbersQueryHandler
    : QueryHandlerBase<ListPortalBaseNumbersQuery, IReadOnlyList<BaseNumberHistoryDto>?>
{
    private readonly IPortalRepository _portal;
    private readonly IDocumentRepository _documents;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ListPortalBaseNumbersQueryHandler(
        IPortalRepository portal,
        IDocumentRepository documents,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<ListPortalBaseNumbersQuery, IReadOnlyList<BaseNumberHistoryDto>?>> behaviors)
        : base(behaviors)
    {
        _portal = portal;
        _documents = documents;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<IReadOnlyList<BaseNumberHistoryDto>?> HandleCoreAsync(
        ListPortalBaseNumbersQuery query, CancellationToken ct)
    {
        var doc = await PortalService.GetScopedDocumentAsync(_portal, _documents, query.UserId, query.DocumentId, ct);
        if (doc is null)
            return null;

        return DocumentBaseNumberPolicy.BuildHistory(
            doc, doc.BaseNumbers, ServerClock.CurrentYear(_clock, _timeZone));
    }
}
