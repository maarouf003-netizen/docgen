namespace DocGenerator.Application.Services;

public sealed partial class DocumentAppealService : IDocumentAppealService
{
    // ── حرّاس الوصول القرائي للملف الأساس ─────────────────────────────────
    // تُستدعى من مسارات قراءة أخرى (صلاحيات المتحكم/المراسلات/الإحالة) داخل
    // معاملاتها، فتبقى على الخدمة بدل نقلها إلى معالج استعلام مستقل.

    public Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default)
        => _appeals.IsAssignedFollowerAsync(documentId, userId, ct);

    public Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default)
        => _appeals.HasForwardedAppealAsync(documentId, ct);
}
