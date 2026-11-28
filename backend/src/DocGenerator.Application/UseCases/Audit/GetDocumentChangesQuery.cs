using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Audit;

/// <summary>
/// سجل تعديلات ملف محدد على مستوى الحقول: مجموعات مرتبة زمنيًا (الأحدث أولًا)
/// كل مجموعة بإدخال تدقيق واحد وقائمة تغييراته.
/// </summary>
public sealed record GetDocumentChangesQuery(
    int DocumentId,
    int Page,
    int PerPage) : IQuery<PagedResult<DocumentChangeGroupDto>>;

internal sealed class GetDocumentChangesQueryHandler
    : QueryHandlerBase<GetDocumentChangesQuery, PagedResult<DocumentChangeGroupDto>>
{
    private readonly IAuditLogRepository _logs;

    public GetDocumentChangesQueryHandler(
        IAuditLogRepository logs,
        IEnumerable<IPipelineBehavior<GetDocumentChangesQuery, PagedResult<DocumentChangeGroupDto>>> behaviors)
        : base(behaviors)
        => _logs = logs;

    protected override async Task<PagedResult<DocumentChangeGroupDto>> HandleCoreAsync(
        GetDocumentChangesQuery request, CancellationToken ct)
    {
        var documentId = request.DocumentId;
        var page = request.Page;
        var perPage = request.PerPage;

        (page, perPage) = PageRequest.Normalize(page, perPage);

        var (total, items) = await _logs.PageDocumentChangeGroupsAsync(documentId, page, perPage, ct);

        return new PagedResult<DocumentChangeGroupDto>
        {
            Items = items.Select(a => new DocumentChangeGroupDto(
                a.Id,
                a.ActionType ?? string.Empty,
                a.UserName,
                a.Timestamp,
                a.FieldChanges
                    .OrderBy(c => c.Id)
                    .Select(c => new DocumentFieldChangeDto(c.FieldLabel, c.FieldKey, c.OldValue, c.NewValue))
                    .ToList())).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }
}
