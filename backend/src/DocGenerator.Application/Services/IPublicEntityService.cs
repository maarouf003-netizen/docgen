using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>سياق الفاعل: يمرره المتحكم من التوكن بعد فحص الصلاحية العامة.</summary>
public sealed record EntityRegistryActor(
    int UserId,
    string? Name,
    UserRole Role,
    int? BranchId);

/// <summary>معايير قائمة السجل (شاشة الإدارة/البحث).</summary>
public sealed record EntityRegistryListQuery(
    string? Q,
    string? Governorate,
    string? Status,
    bool IncludePending,
    int Page,
    int PerPage,
    /// <summary>شاشة الإدارة ترى الموقوف أيضًا؛ نافذة الاختيار وربط المندوبين لا يريانه (افتراضيًا يُرى).</summary>
    bool IncludeInactive = true,
    /// <summary>فلترة صريحة بفرع بعينه (اختياري) — مثل «الجهة الأم» لعرض الجهة الأساسية دون فرع.</summary>
    string? BranchName = null);

public interface IPublicEntityService
{
    Task<PagedResult<PublicEntityEntryDto>> ListAsync(EntityRegistryListQuery query, CancellationToken ct = default);

    /// <summary>قيود بانتظار مراجعة رئيس القسم ضمن نطاقه (المدير/المشرف يرىان الكل).</summary>
    Task<List<PublicEntityEntryDto>> ListNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>عدد قيود «بانتظار المراجعة» ضمن نطاق الفاعل — شارة خفيفة دون تحميل القائمة.</summary>
    Task<int> CountNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>سجل تغييرات الجهات — مصدره PublicEntityChangeEvent فقط (د5 §7).
    /// نطاق رئيس القسم محافظته فقط (الجبر الخادمي يتجاهل پارامتر العميل).</summary>
    Task<PagedResult<EntityChangeEventDto>> ListChangeEventsAsync(EntityChangeEventQuery query, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>تصدير سجل التغييرات إلى Excel (نفس فلاتر القائمة ونطاق رئيس القسم).</summary>
    Task<Stream> ExportChangeEventsAsync(EntityChangeEventQuery query, EntityRegistryActor actor, CancellationToken ct = default);

    Task<ImportPreviewResponse> PreviewImportAsync(CancellationToken ct = default);

    /// <summary>قائمة المجموعات (الهويات الأم) مع ترقيم وبحث — للعرض المستقل وتوحيد التسمية/إدارة الفروع.</summary>
    Task<PagedResult<PublicEntityGroupDto>> ListGroupsAsync(EntityGroupListQuery query, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>أقرب المشابهات لجهة محددة (تبويب «كافة الجهات» عند تحديد جهة واحدة).</summary>
    Task<SimilarToResponse> FindSimilarToGroupAsync(int groupId, double threshold, int maxResults, CancellationToken ct = default);

    /// <summary>قيود مجموعة واحدة — لرئيس القسم (محافظته فقط) ولوحة إدارة الفروع.</summary>
    Task<IReadOnlyList<PublicEntityEntryDto>> ListEntriesByGroupAsync(int groupId, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>قائمة اقتراحات تعديل الجهة الأم (تبويب الإدارة / حالة المعلّق في نافذة الفروع).</summary>
    Task<PagedResult<ParentEditSuggestionDto>> ListParentEditSuggestionsAsync(ParentEditSuggestionListQuery query, EntityRegistryActor actor, CancellationToken ct = default);
}
