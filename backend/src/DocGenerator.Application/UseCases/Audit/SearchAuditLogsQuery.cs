using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Audit;

/// <summary>
/// سجل التدقيق العام مرقّمًا — يقبل نطاق فرع/شعبة من حارس المتحكم (RF-007/F6).
/// </summary>
public sealed record SearchAuditLogsQuery(
    string? UserName,
    string? ActionType,
    int Page,
    int PerPage,
    int? ScopeBranchId,
    int? ScopeSectionId) : IQuery<PagedResult<AuditLogDto>>;

internal sealed class SearchAuditLogsQueryHandler
    : QueryHandlerBase<SearchAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private readonly IAuditLogRepository _logs;

    public SearchAuditLogsQueryHandler(
        IAuditLogRepository logs,
        IEnumerable<IPipelineBehavior<SearchAuditLogsQuery, PagedResult<AuditLogDto>>> behaviors)
        : base(behaviors)
        => _logs = logs;

    protected override async Task<PagedResult<AuditLogDto>> HandleCoreAsync(
        SearchAuditLogsQuery request, CancellationToken ct)
    {
        var userName = request.UserName;
        var actionType = request.ActionType;
        var page = request.Page;
        var perPage = request.PerPage;
        var scopeBranchId = request.ScopeBranchId;
        var scopeSectionId = request.ScopeSectionId;

        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _logs.SearchAsync(userName, actionType, page, perPage, ct, scopeBranchId, scopeSectionId);

        return new PagedResult<AuditLogDto>
        {
            Items = items.Select(a => new AuditLogDto(
                a.Id, a.Timestamp, a.UserName, a.ActionType, a.Details, a.DocumentId, a.DocumentType)).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }
}
