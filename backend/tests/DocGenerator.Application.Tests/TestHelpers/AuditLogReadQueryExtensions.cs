using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Audit;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري لمعالجات قراءات سجل التدقيق (`P4-01cw`): يحافظ على توقيعات
/// خدمة التدقيق القديمة فوق سجل المعالجات — منطق الاختبارات كما هو.
/// </summary>
public static class AuditLogReadQueryExtensions
{
    public static Task<PagedResult<AuditLogDto>> SearchAsync(
        this AuditLogReadHandlers handlers,
        string? userName,
        string? actionType,
        int page,
        int perPage,
        CancellationToken ct = default,
        int? scopeBranchId = null,
        int? scopeSectionId = null)
        => handlers.Search.HandleAsync(
            new SearchAuditLogsQuery(
                userName, actionType, page, perPage, scopeBranchId, scopeSectionId), ct);

    public static Task<PagedResult<DocumentChangeGroupDto>> GetDocumentChangesAsync(
        this AuditLogReadHandlers handlers,
        int documentId,
        int page,
        int perPage,
        CancellationToken ct = default)
        => handlers.Changes.HandleAsync(new GetDocumentChangesQuery(documentId, page, perPage), ct);
}
