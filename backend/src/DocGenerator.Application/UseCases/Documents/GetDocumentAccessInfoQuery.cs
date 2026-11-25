using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// إسقاط خفيف (F-01) لفحص الوصول: لا يحمّل شجرة المستند الكاملة.
/// </summary>
public sealed record GetDocumentAccessInfoQuery(int DocumentId) : IQuery<DocumentAccessInfo?>;

internal sealed class GetDocumentAccessInfoQueryHandler
    : QueryHandlerBase<GetDocumentAccessInfoQuery, DocumentAccessInfo?>
{
    private readonly IDocumentRepository _documents;

    public GetDocumentAccessInfoQueryHandler(
        IDocumentRepository documents,
        IEnumerable<IPipelineBehavior<GetDocumentAccessInfoQuery, DocumentAccessInfo?>> behaviors)
        : base(behaviors)
        => _documents = documents;

    protected override Task<DocumentAccessInfo?> HandleCoreAsync(
        GetDocumentAccessInfoQuery query, CancellationToken ct)
        => _documents.GetAccessInfoAsync(query.DocumentId, ct);
}
