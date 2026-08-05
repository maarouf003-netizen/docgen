using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogDto>> SearchAsync(
        string? userName, string? actionType, int page, int perPage, CancellationToken ct = default);
}

public sealed class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _logs;

    public AuditLogService(IAuditLogRepository logs) => _logs = logs;

    public async Task<PagedResult<AuditLogDto>> SearchAsync(
        string? userName, string? actionType, int page, int perPage, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _logs.SearchAsync(userName, actionType, page, perPage, ct);

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
