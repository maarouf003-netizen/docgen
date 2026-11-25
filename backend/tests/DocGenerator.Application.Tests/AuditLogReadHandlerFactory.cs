using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Audit;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>حزمة معالجات قراءات سجل التدقيق (`P4-01cw`) على قاعدة اختبارية واحدة.</summary>
public sealed record AuditLogReadHandlers(
    IQueryHandler<SearchAuditLogsQuery, PagedResult<AuditLogDto>> Search,
    IQueryHandler<GetDocumentChangesQuery, PagedResult<DocumentChangeGroupDto>> Changes);

public static class AuditLogReadHandlerFactory
{
    public static AuditLogReadHandlers Create(DocGeneratorDbContext db)
        => new(
            new SearchAuditLogsQueryHandler(
                new AuditLogRepository(db),
                Array.Empty<IPipelineBehavior<SearchAuditLogsQuery, PagedResult<AuditLogDto>>>()),
            new GetDocumentChangesQueryHandler(
                new AuditLogRepository(db),
                Array.Empty<IPipelineBehavior<GetDocumentChangesQuery, PagedResult<DocumentChangeGroupDto>>>()));
}
