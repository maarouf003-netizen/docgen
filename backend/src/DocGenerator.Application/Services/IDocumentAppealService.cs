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
    /// <summary>تسطير استئناف جديد على ملف يملکه المحامي (ليس تحت الرفع).</summary>
    Task<AppealDto> CreateAsync(int documentId, UpsertAppealRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>تعديل استئناف قبل الإسناد — المنشئ فقط.</summary>
    Task<AppealDto?> UpdateAsync(int appealId, UpsertAppealRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>حذف استئناف قبل الإسناد — المنشئ فقط.</summary>
    Task<bool> DeleteAsync(int appealId, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>استئنافات ملف (بطاقة «الاستئنافات» في وقوعات الملف).</summary>
    Task<List<AppealDto>> ListForDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>بحث/قائمة الاستئنافات لنطاق رؤية محدد (صفحة «الاستئنافات»).</summary>
    Task<PagedResult<AppealDto>> SearchAsync(
        string? query, string? status, int? visibleBranchId, int? visibleUserId,
        int page, int perPage, CancellationToken ct = default);

    /// <summary>تفاصيل استئناف بمعرفه.</summary>
    Task<AppealDto?> GetAsync(int appealId, CancellationToken ct = default);

    /// <summary>كيان الاستئناف بروابطه (للتحقق من صلاحية العرض في المتحكم).</summary>
    Task<DocGenerator.Domain.Entities.DocumentAppeal?> GetEntityAsync(int appealId, CancellationToken ct = default);

    /// <summary>تحديث حقول القيد (المحكمة/رقم الأساس/السنة/تاريخ الإقرار/النوع) — المحامي المتابع.</summary>
    Task<AppealDto?> UpdateRegistrationAsync(int appealId, UpdateAppealRegistrationRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>حسم الاستئناف برقم قرار الحسم وتاريخه ومنطوقه ونتيجته — المحامي المتابع.</summary>
    Task<AppealDto?> DecideAsync(int appealId, DecideAppealRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>شطب الاستئناف بتاريخ الشطب ورقم قرار الشطب — المحامي المتابع.</summary>
    Task<AppealDto?> StrikeAsync(int appealId, StrikeAppealRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>إسناد الاستئناف إلى محامي الفرع للمتابعة — رئيس القسم (فرعه).</summary>
    Task<AppealDto?> AssignAsync(int appealId, AssignAppealRequest request, int userId, int? headBranchId, string? actorName, CancellationToken ct = default);

    /// <summary>نقل استئناف مفرد بين محامي الفرع — رئيس القسم (فرعه).</summary>
    Task<AppealDto?> TransferAsync(int appealId, TransferAppealRequest request, int userId, int? headBranchId, string? actorName, CancellationToken ct = default);

    /// <summary>نقل كل استئنافات محامٍ إلى محامٍ آخر ضمن الفرع — رئيس القسم (فرعه).</summary>
    Task<int> TransferAllAsync(TransferAllAppealsRequest request, int? headBranchId, string? actorName, CancellationToken ct = default);

    /// <summary>عدد استئنافات محامٍ ضمن فرع رئيس القسم — لمعاينة النقل الجملة.</summary>
    Task<int> CountByAssigneeForHeadAsync(int assigneeId, int? headBranchId, CancellationToken ct = default);

    /// <summary>تاريخ أرقام الأساس الاستئنافية لكل السنوات.</summary>
    Task<List<AppealBaseNumberHistoryDto>> GetBaseNumberHistoryAsync(int appealId, CancellationToken ct = default);

    /// <summary>إدخال/تدوير رقم الأساس الاستئنافي لسنة التدوير الحالية — المحامي المتابع أو المنشئ.</summary>
    Task SaveBaseNumbersAsync(int appealId, SaveAppealBaseNumbersRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>هل المستخدم هو المحامي المسند إليه متابعة استئناف على الملف؟ (وصول قرائي للملف الأساس).</summary>
    Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default);

    // ── الإجراءات والملاحظات المستقلة للاستئناف ────────────────────────────

    Task<List<AppealActionDto>> GetActionsAsync(int appealId, CancellationToken ct = default);
    Task<AppealActionDto> AddActionAsync(int appealId, AddAppealActionRequest request, int userId, string? actorName, CancellationToken ct = default);
    Task<AppealActionDto?> UpdateActionAsync(int appealId, int actionId, UpdateAppealActionRequest request, int userId, string? actorName, CancellationToken ct = default);
    Task<bool> DeleteActionAsync(int appealId, int actionId, int userId, string? actorName, CancellationToken ct = default);
    Task<bool> ClearReminderAsync(int appealId, int actionId, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>تذكيرات إجراءات الاستئنافات التي يتابعها المحامي (بطاقة التذكيرات).</summary>
    Task<List<AppealReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default);
}
