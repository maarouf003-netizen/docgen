using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogDto>> SearchAsync(
        string? userName, string? actionType, int page, int perPage, CancellationToken ct = default);

    /// <summary>
    /// سجل تعديلات ملف محدد على مستوى الحقول: مجموعات مرتبة زمنيًا (الأحدث أولًا)
    /// كل مجموعة بإدخال تدقيق واحد وقائمة تغييراته.
    /// </summary>
    Task<PagedResult<DocumentChangeGroupDto>> GetDocumentChangesAsync(
        int documentId, int page, int perPage, CancellationToken ct = default);
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

    public async Task<PagedResult<DocumentChangeGroupDto>> GetDocumentChangesAsync(
        int documentId, int page, int perPage, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

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
