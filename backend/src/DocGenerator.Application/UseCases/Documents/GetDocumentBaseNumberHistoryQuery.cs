using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// تاريخ أرقام الأساس للملف (سنة + رقم) مرتبًا تنازليًا بالسنوات — لعرضه عند الضغط
/// على رقم الملف.
/// </summary>
public sealed record GetDocumentBaseNumberHistoryQuery(int DocumentId)
    : IQuery<List<BaseNumberHistoryDto>>;

internal sealed class GetDocumentBaseNumberHistoryQueryHandler
    : QueryHandlerBase<GetDocumentBaseNumberHistoryQuery, List<BaseNumberHistoryDto>>
{
    private readonly IDocumentNumberRegistry _numberRegistry;

    public GetDocumentBaseNumberHistoryQueryHandler(
        IDocumentNumberRegistry numberRegistry,
        IEnumerable<IPipelineBehavior<GetDocumentBaseNumberHistoryQuery, List<BaseNumberHistoryDto>>> behaviors)
        : base(behaviors)
        => _numberRegistry = numberRegistry;

    protected override async Task<List<BaseNumberHistoryDto>> HandleCoreAsync(
        GetDocumentBaseNumberHistoryQuery query, CancellationToken ct)
    {
        var history = await _numberRegistry.GetHistoryAsync(
            query.DocumentId, includeDeletedDocument: false, ct);
        return history.ToList();
    }
}
