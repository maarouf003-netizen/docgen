using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلام سجل التدقيق مع ترقيم صفحات على مستوى قاعدة البيانات.
/// FR-011: قراءة فقط عمدًا — لا ترث `IRepository&lt;AuditLog&gt;` فلا تعرض
/// `Update`/`Remove` لعزل الكتابة (الكتابة عبر `IAuditLogger`، والتنقية دفتر خارجي).
/// </summary>
public interface IAuditLogRepository
{
    Task<(int TotalCount, List<AuditLog> Items)> SearchAsync(
        string? userName,
        string? actionType,
        int page,
        int perPage,
        CancellationToken ct = default,
        int? scopeBranchId = null,
        int? scopeSectionId = null);

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
