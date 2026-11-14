namespace DocGenerator.Application.Common;

/// <summary>
/// إسقاط قراءة خفيف لفحص وصول المستند (F-01): الحقول الخمسة التي تعتمد عليها
/// قاعدة <c>DocumentsController.CanAccessAsync</c> حصرًا — بدل تحميل شجرة
/// <c>Document</c> الكاملة (نحو 20 <c>Include</c>) لمجرد فحص صلاحية.
/// </summary>
public sealed record DocumentAccessInfo(
    int Id,
    int CreatedById,
    int? BranchId,
    int? ExecutionCircuitId,
    int? SectionId);
