using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة الاستئنافات على الملفات التنفيذية: تسطير الاستئناف من محامي الملف المالك
/// (مستأنِفين أو مستأنف علينا بلقطتي أطراف مثبّتة)، وإسناده لمحامٍ للمتابعة من رئيس
/// القسم، وتحديث حقول القيد والحسم والشطب من المحامي المتابع، ونقل الاستئنافات
/// بين المحامين (فرديًا وجملةً)، وتدوير رقم الأساس الاستئنافي سنويًا، وإدارة قائمة
/// الإجراءات والملاحظات المستقلة للاستئناف مع تذكيراتها.
/// كل كتابة ضمن معاملة واحدة مع سجل التدقيق وتحقق صارم من الصلاحيات والحالات.
/// </summary>
public interface IDocumentAppealService
{
    /// <summary>استئنافات ملف (بطاقة «الاستئنافات» في وقوعات الملف).</summary>
    Task<List<AppealDto>> ListForDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>بحث/قائمة الاستئنافات لنطاق رؤية محدد (صفحة «الاستئنافات» — §5).</summary>
    Task<PagedResult<AppealDto>> SearchAsync(
        string? query, string? status, int? visibleBranchId, int? visibleUserId,
        int? ownerSectionId, int page, int perPage, CancellationToken ct = default);

    /// <summary>تفاصيل استئناف بمعرفه.</summary>
    Task<AppealDto?> GetAsync(int appealId, CancellationToken ct = default);

    /// <summary>كيان الاستئناف بروابطه (للتحقق من صلاحية العرض في المتحكم).</summary>
    Task<DocGenerator.Domain.Entities.DocumentAppeal?> GetEntityAsync(int appealId, CancellationToken ct = default);

    /// <summary>عدد استئنافات محامٍ ضمن نطاق الرئيس — لمعاينة النقل الجملة (تطابق المنقول).</summary>
    Task<int> CountByAssigneeForHeadAsync(int assigneeId, int? headBranchId, int? ownerSectionId, CancellationToken ct = default);

    /// <summary>تاريخ أرقام الأساس الاستئنافية لكل السنوات.</summary>
    Task<List<AppealBaseNumberHistoryDto>> GetBaseNumberHistoryAsync(int appealId, CancellationToken ct = default);

    /// <summary>هل المستخدم هو المحامي المسند إليه متابعة استئناف على الملف؟ (وصول قرائي للملف الأساس).</summary>
    Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default);

    /// <summary>الاستثناء القرائي للإحالة (قرار §2.22): إحالة مفتوحة لرئيس القسم على الملف.</summary>
    Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default);

    // ── الإجراءات والملاحظات المستقلة للاستئناف ────────────────────────────

    Task<List<AppealActionDto>> GetActionsAsync(int appealId, CancellationToken ct = default);

    /// <summary>تذكيرات إجراءات الاستئنافات التي يتابعها المحامي (بطاقة التذكيرات).</summary>
    Task<List<AppealReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default);
}
