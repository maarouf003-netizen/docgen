using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Portal;

/// <summary>الإجراءات التنفيذية القرائية (نوع action فقط، الأحدث أولًا) — null خارج النطاق.</summary>
public sealed record ListPortalExecutionActionsQuery(int UserId, int DocumentId)
    : IQuery<IReadOnlyList<PortalExecutionActionDto>?>;

internal sealed class ListPortalExecutionActionsQueryHandler
    : QueryHandlerBase<ListPortalExecutionActionsQuery, IReadOnlyList<PortalExecutionActionDto>?>
{
    private readonly IPortalRepository _portal;
    private readonly IDocumentRepository _documents;

    public ListPortalExecutionActionsQueryHandler(
        IPortalRepository portal,
        IDocumentRepository documents,
        IEnumerable<IPipelineBehavior<ListPortalExecutionActionsQuery, IReadOnlyList<PortalExecutionActionDto>?>> behaviors)
        : base(behaviors)
    {
        _portal = portal;
        _documents = documents;
    }

    protected override async Task<IReadOnlyList<PortalExecutionActionDto>?> HandleCoreAsync(
        ListPortalExecutionActionsQuery query, CancellationToken ct)
    {
        var doc = await PortalService.GetScopedDocumentAsync(_portal, _documents, query.UserId, query.DocumentId, ct);
        if (doc is null)
            return null;

        // النوع action فقط (الملاحظات الداخلية لا تصل إلى البوابة أصلًا) والأحدث أولًا،
        // وكسر التعادل الزمني بـ Id الأحدث (ف11: CreatedAt ثم Id).
        return doc.ExecutionActions
            .Where(a => a.Type == "action")
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new PortalExecutionActionDto(a.Id, a.Text, a.ActionDate, a.CreatedBy?.FullName, a.CreatedAt))
            .ToList();
    }
}
