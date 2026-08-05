using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلام سجل التدقيق مع ترقيم صفحات على مستوى قاعدة البيانات.
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<(int TotalCount, List<AuditLog> Items)> SearchAsync(
        string? userName,
        string? actionType,
        int page,
        int perPage,
        CancellationToken ct = default);
}
