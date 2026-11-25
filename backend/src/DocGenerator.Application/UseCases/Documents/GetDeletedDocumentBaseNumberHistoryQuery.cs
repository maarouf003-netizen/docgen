using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>سجل أرقام ملف محذوف (IgnoreQueryFilters) — لصفحة المحذوفات.</summary>
public sealed record GetDeletedDocumentBaseNumberHistoryQuery(int DocumentId)
    : IQuery<List<BaseNumberHistoryDto>>;

internal sealed class GetDeletedDocumentBaseNumberHistoryQueryHandler
    : QueryHandlerBase<GetDeletedDocumentBaseNumberHistoryQuery, List<BaseNumberHistoryDto>>
{
    private readonly IDocumentNumberRegistry _numberRegistry;

    public GetDeletedDocumentBaseNumberHistoryQueryHandler(
        IDocumentNumberRegistry numberRegistry,
        IEnumerable<IPipelineBehavior<GetDeletedDocumentBaseNumberHistoryQuery, List<BaseNumberHistoryDto>>> behaviors)
        : base(behaviors)
        => _numberRegistry = numberRegistry;

    protected override async Task<List<BaseNumberHistoryDto>> HandleCoreAsync(
        GetDeletedDocumentBaseNumberHistoryQuery query, CancellationToken ct)
    {
        var history = await _numberRegistry.GetHistoryAsync(
            query.DocumentId, includeDeletedDocument: true, ct);
        return history.ToList();
    }
}
