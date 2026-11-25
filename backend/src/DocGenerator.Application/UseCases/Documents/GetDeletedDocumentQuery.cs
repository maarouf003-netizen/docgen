using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// قراءة ملف محذوف منطقيًا بالمعرّف (متجاوزًا `Query Filter`) — صفحة المحذوفات
/// والاستعادة. نتيجة `null` تعني غير موجود أو غير محذوف.
/// </summary>
public sealed record GetDeletedDocumentQuery(int DocumentId) : IQuery<DocumentResponse?>;

internal sealed class GetDeletedDocumentQueryHandler
    : QueryHandlerBase<GetDeletedDocumentQuery, DocumentResponse?>
{
    private readonly IDocumentRepository _documents;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetDeletedDocumentQueryHandler(
        IDocumentRepository documents,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetDeletedDocumentQuery, DocumentResponse?>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<DocumentResponse?> HandleCoreAsync(
        GetDeletedDocumentQuery query, CancellationToken ct)
    {
        var doc = await _documents.GetDeletedByIdAsync(query.DocumentId, ct);
        return doc is null || !doc.IsDeleted
            ? null
            : DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
    }
}
