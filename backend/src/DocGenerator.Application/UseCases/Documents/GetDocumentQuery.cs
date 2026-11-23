using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>قراءة ملف واحد بالمعرّف — نتيجة `null` تعني غير موجود (يُترجم 404).</summary>
public sealed record GetDocumentQuery(int DocumentId) : IQuery<DocumentResponse?>;

public sealed class GetDocumentQueryHandler : QueryHandlerBase<GetDocumentQuery, DocumentResponse?>
{
    private readonly IDocumentService _documents;

    public GetDocumentQueryHandler(
        IDocumentService documents,
        IEnumerable<IPipelineBehavior<GetDocumentQuery, DocumentResponse?>> behaviors)
        : base(behaviors)
        => _documents = documents;

    protected override Task<DocumentResponse?> HandleCoreAsync(GetDocumentQuery query, CancellationToken ct)
        => _documents.GetAsync(query.DocumentId, ct);
}
