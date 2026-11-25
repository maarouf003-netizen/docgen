using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Appeals;

namespace DocGenerator.Application.UseCases.Portal;

/// <summary>تفاصيل استئنافات الملف (رأي المحامي وملاحظاته مخفيّان عنه في البوابة) — null خارج النطاق.</summary>
public sealed record ListPortalAppealDetailsQuery(int UserId, int DocumentId)
    : IQuery<IReadOnlyList<AppealDto>?>;

internal sealed class ListPortalAppealDetailsQueryHandler
    : QueryHandlerBase<ListPortalAppealDetailsQuery, IReadOnlyList<AppealDto>?>
{
    private readonly IPortalRepository _portal;
    private readonly IDocumentRepository _documents;
    private readonly IQueryHandler<ListAppealsForDocumentQuery, List<AppealDto>> _appealReads;

    public ListPortalAppealDetailsQueryHandler(
        IPortalRepository portal,
        IDocumentRepository documents,
        IQueryHandler<ListAppealsForDocumentQuery, List<AppealDto>> appealReads,
        IEnumerable<IPipelineBehavior<ListPortalAppealDetailsQuery, IReadOnlyList<AppealDto>?>> behaviors)
        : base(behaviors)
    {
        _portal = portal;
        _documents = documents;
        _appealReads = appealReads;
    }

    protected override async Task<IReadOnlyList<AppealDto>?> HandleCoreAsync(
        ListPortalAppealDetailsQuery query, CancellationToken ct)
    {
        var doc = await PortalService.GetScopedDocumentAsync(_portal, _documents, query.UserId, query.DocumentId, ct);
        if (doc is null)
            return null;

        var appeals = await _appealReads.HandleAsync(new ListAppealsForDocumentQuery(query.DocumentId), ct);

        // رأي المحامي الداخلي (دفاعه) وملاحظاته الحرة لا يظهران للمندوب (ق10 الموسّعة) —
        // حقلان حرّان من إدخال المحامي بلا ضمان بنيوي بأنهما وقائع قضية، فيُصفَّران معًا.
        // بقية الحقول (اسم المحامي المتابع وسطر «سطّره») تبقى ظاهرة (ق9).
        return appeals.Select(a => a with { DefenseOpinion = null, Notes = null }).ToList();
    }
}
