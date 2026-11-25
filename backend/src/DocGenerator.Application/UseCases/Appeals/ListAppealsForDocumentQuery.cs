using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>استئنافات ملف (بطاقة «الاستئنافات» في وقوعات الملف).</summary>
public sealed record ListAppealsForDocumentQuery(int DocumentId) : IQuery<List<AppealDto>>;

internal sealed class ListAppealsForDocumentQueryHandler
    : QueryHandlerBase<ListAppealsForDocumentQuery, List<AppealDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IAppealRepository _appeals;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ListAppealsForDocumentQueryHandler(
        IDocumentRepository documents,
        IAppealRepository appeals,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<ListAppealsForDocumentQuery, List<AppealDto>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _appeals = appeals;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<List<AppealDto>> HandleCoreAsync(
        ListAppealsForDocumentQuery query, CancellationToken ct)
    {
        var documentId = query.DocumentId;
        // فحص وجود رخيص (بلا تحميل الشجرة الكاملة للملف).
        if (!await _documents.ExistsAsync(documentId, ct))
            throw new ArgumentException("الملف غير موجود");
        var items = await _appeals.ListByDocumentAsync(documentId, ct);
        return items.Select(a => DocumentAppealService.ToDto(a, ServerClock.CurrentYear(_clock, _timeZone))).ToList();
    }
}
