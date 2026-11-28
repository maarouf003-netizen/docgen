using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// جدول تدوير أرقام الأساس للمحامي (ترحّلي): ملفاته المؤهلة مع رقم أساس السنة
/// الحالية إن وُجد.
/// </summary>
public sealed record GetDocumentRotationListQuery(
    int UserId,
    int Page,
    int PerPage) : IQuery<PagedResult<RotationDocumentDto>>;

internal sealed class GetDocumentRotationListQueryHandler
    : QueryHandlerBase<GetDocumentRotationListQuery, PagedResult<RotationDocumentDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentNumberRegistry _numberRegistry;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetDocumentRotationListQueryHandler(
        IDocumentRepository documents,
        IDocumentNumberRegistry numberRegistry,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetDocumentRotationListQuery, PagedResult<RotationDocumentDto>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _numberRegistry = numberRegistry;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<PagedResult<RotationDocumentDto>> HandleCoreAsync(
        GetDocumentRotationListQuery query, CancellationToken ct)
    {
        var (page, perPage) = PageRequest.Normalize(query.Page, query.PerPage);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        var (total, docs) = await _documents.GetRotationCandidatesAsync(
            query.UserId, currentYear, page, perPage, _numberRegistry.AllowsCircuitlessLegacy, ct);
        var items = docs
            .Select(d => new RotationDocumentDto(
                d.Id,
                d.Court,
                d.BorrowerName,
                d.BorrowerFather,
                d.BorrowerFamily,
                d.FileNumber,
                d.FileType,
                EffectiveFileIdentity.Latest(d, currentYear)?.BaseNumber,
                DocumentText.RotationDisplayName(d),
                EffectiveFileIdentity.Number(d, currentYear),
                EffectiveFileIdentity.Year(d, currentYear)))
            .ToList();

        return new PagedResult<RotationDocumentDto>
        {
            Items = items,
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }
}
