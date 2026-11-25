using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>قراءة ملف واحد بالمعرّف — نتيجة `null` تعني غير موجود (يُترجم 404).</summary>
public sealed record GetDocumentQuery(int DocumentId) : IQuery<DocumentResponse?>;

internal sealed class GetDocumentQueryHandler : QueryHandlerBase<GetDocumentQuery, DocumentResponse?>
{
    private readonly IDocumentRepository _documents;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetDocumentQueryHandler(
        IDocumentRepository documents,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetDocumentQuery, DocumentResponse?>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<DocumentResponse?> HandleCoreAsync(
        GetDocumentQuery query, CancellationToken ct)
    {
        var doc = await _documents.GetByIdAsync(query.DocumentId, ct);
        return doc is null ? null : DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
    }
}
