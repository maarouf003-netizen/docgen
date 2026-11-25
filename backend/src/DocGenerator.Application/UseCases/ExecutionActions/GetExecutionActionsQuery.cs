using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionActions;

/// <summary>
/// إجراءات/ملاحظات ملف مرتبة بالأحدث إنشاءً — قراءة صرفة. الملف غير الموجود
/// يعيد قائمة فارغة (لا 404) كما الأصل.
/// </summary>
public sealed record GetExecutionActionsQuery(int DocumentId) : IQuery<List<ExecutionActionDto>>;

internal sealed class GetExecutionActionsQueryHandler
    : QueryHandlerBase<GetExecutionActionsQuery, List<ExecutionActionDto>>
{
    private readonly IDocumentRepository _documents;

    public GetExecutionActionsQueryHandler(
        IDocumentRepository documents,
        IEnumerable<IPipelineBehavior<GetExecutionActionsQuery, List<ExecutionActionDto>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
    }

    protected override async Task<List<ExecutionActionDto>> HandleCoreAsync(
        GetExecutionActionsQuery query, CancellationToken ct)
    {
        var doc = await _documents.GetByIdAsync(query.DocumentId, ct);
        if (doc is null)
            return new List<ExecutionActionDto>();
        return doc.ExecutionActions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => ExecutionActionService.ToDto(a, a.CreatedBy?.FullName))
            .ToList();
    }
}
