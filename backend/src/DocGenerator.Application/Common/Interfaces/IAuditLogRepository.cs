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

    /// <summary>
    /// صفحة «سجل التعديلات» لملف محدد: إدخالات التدقيق التي لها تغييرات حقول،
    /// الأحدث أولًا، مع صفوف التغييرات التابعة لكل إدخال.
    /// </summary>
    Task<(int TotalCount, List<AuditLog> Items)> PageDocumentChangeGroupsAsync(
        int documentId,
        int page,
        int perPage,
        CancellationToken ct = default);
}
