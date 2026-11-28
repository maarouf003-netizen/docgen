using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>بحث ترحّلي عن الملفات المحذوفة لنطاق رؤية محدد (صفحة «المحذوفات»).</summary>
public sealed record SearchDeletedDocumentsQuery(
    string? Query, int Page, int PerPage, int? VisibleBranchId = null,
    int? VisibleUserId = null, int? OwnerSectionId = null)
    : IQuery<PagedResult<DocumentResponse>>;

internal sealed class SearchDeletedDocumentsQueryHandler
    : QueryHandlerBase<SearchDeletedDocumentsQuery, PagedResult<DocumentResponse>>
{
    private readonly IDocumentRepository _documents;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public SearchDeletedDocumentsQueryHandler(
        IDocumentRepository documents,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<SearchDeletedDocumentsQuery, PagedResult<DocumentResponse>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<PagedResult<DocumentResponse>> HandleCoreAsync(
        SearchDeletedDocumentsQuery query, CancellationToken ct)
    {
        var (page, perPage) = PageRequest.Normalize(query.Page, query.PerPage);

        var (total, items) = await _documents.SearchDeletedAsync(query.Query, query.VisibleBranchId, query.VisibleUserId, page, perPage, ct, query.OwnerSectionId);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(d => DocumentResponse.FromEntity(d, ServerClock.CurrentYear(_clock, _timeZone))).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }
}
