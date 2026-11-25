using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>بحث/قائمة الملفات التنفيذية لنطاق رؤية محدد — قراءة صرفة مع شارة الاستئناف.</summary>
public sealed record SearchDocumentsQuery(
    string? Query, string? Status, string? Applicant, string? Court, string? Lawyer,
    string? Branch, string? AdministrativeBranch, string? ExecutedEntity, string? PublicEntityBranch,
    int Page, int PerPage, int? VisibleBranchId = null, int? VisibleUserId = null,
    int? OwnerSectionId = null) : IQuery<PagedResult<DocumentResponse>>;

internal sealed class SearchDocumentsQueryHandler
    : QueryHandlerBase<SearchDocumentsQuery, PagedResult<DocumentResponse>>
{
    private readonly IDocumentRepository _documents;
    private readonly IAppealRepository _appeals;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public SearchDocumentsQueryHandler(
        IDocumentRepository documents,
        IAppealRepository appeals,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<SearchDocumentsQuery, PagedResult<DocumentResponse>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _appeals = appeals;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<PagedResult<DocumentResponse>> HandleCoreAsync(
        SearchDocumentsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage, 1, 100);

        var (total, items) = await _documents.SearchAsync(
            query.Query, query.Status, query.Applicant, query.Court, query.Lawyer, query.Branch, query.AdministrativeBranch, query.ExecutedEntity, query.PublicEntityBranch, query.VisibleBranchId, query.VisibleUserId, page, perPage, ct, query.OwnerSectionId);

        var result = new PagedResult<DocumentResponse>
        {
            Items = items.Select(d => DocumentResponse.FromEntity(d, ServerClock.CurrentYear(_clock, _timeZone))).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
        await DocumentSearchHelpers.EnrichAppealFlagsAsync(result.Items, _appeals, ct);
        return result;
    }
}
