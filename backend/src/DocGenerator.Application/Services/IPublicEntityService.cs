using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
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

    Task<PublicEntityEntryDto> CreateAsync(CreatePublicEntityRequest request, EntityRegistryActor actor, CancellationToken ct = default);
    Task<PublicEntityEntryDto?> UpdateAsync(int entryId, UpdatePublicEntityRequest request, EntityRegistryActor actor, CancellationToken ct = default);
    Task<PublicEntityEntryDto?> AddAliasAsync(int entryId, AddPublicEntityAliasRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>قيود بانتظار مراجعة رئيس القسم ضمن نطاقه (المدير/المشرف يرىان الكل).</summary>
    Task<List<PublicEntityEntryDto>> ListNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>عدد قيود «بانتظار المراجعة» ضمن نطاق الفاعل — شارة خفيفة دون تحميل القائمة.</summary>
    Task<int> CountNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>سجل تغييرات الجهات — مصدره PublicEntityChangeEvent فقط (د5 §7).
    /// نطاق رئيس القسم محافظته فقط (الجبر الخادمي يتجاهل پارامتر العميل).</summary>
    Task<PagedResult<EntityChangeEventDto>> ListChangeEventsAsync(EntityChangeEventQuery query, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>تصدير سجل التغييرات إلى Excel (نفس فلاتر القائمة ونطاق رئيس القسم).</summary>
    Task<byte[]> ExportChangeEventsAsync(EntityChangeEventQuery query, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>اعتماد قيد كما هو: يقفل مراجعته دون أي تعديل ولا إشعار للمُدخِل.</summary>
    Task<PublicEntityEntryDto?> ApproveReviewAsync(int entryId, EntityRegistryActor actor, CancellationToken ct = default);

    Task<ImportPreviewResponse> PreviewImportAsync(CancellationToken ct = default);
    Task<ImportCommitResultDto> CommitImportAsync(ImportCommitRequest request, int actorUserId, string? actorName, CancellationToken ct = default);

    /// <summary>نقل قيد من هوية أم إلى أخرى أو طيّه في قيد مطابق (د3).</summary>
    Task<MoveEntryResponse> MoveEntryAsync(int entryId, MoveEntryRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>نقل جميع قيود هوية أم إلى هوية أم أخرى (د3 — الوضع أ فقط).</summary>
    Task<MoveAllEntriesResponse> MoveAllEntriesAsync(MoveAllEntriesRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>معاينة دمج جهات متعددة في هوية واحدة (د5 §4).</summary>
    Task<MergePreviewResponse> PreviewMergeAsync(MergePreviewRequest request, CancellationToken ct = default);

    /// <summary>تنفيذ دمج جهات متعددة في هوية واحدة (د5 §4).</summary>
    Task<MergeCommitResponse> CommitMergeAsync(MergeCommitRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>قائمة المجموعات (الهويات الأم) مع ترقيم وبحث — للعرض المستقل وتوحيد التسمية/إدارة الفروع.</summary>
    Task<PagedResult<PublicEntityGroupDto>> ListGroupsAsync(EntityGroupListQuery query, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>أقرب المشابهات لجهة محددة (تبويب «كافة الجهات» عند تحديد جهة واحدة).</summary>
    Task<SimilarToResponse> FindSimilarToGroupAsync(int groupId, double threshold, int maxResults, CancellationToken ct = default);

    /// <summary>معاينة توحيد التسمية N←1 (المدير/المشرف — بلا هجرة ملفات).</summary>
    Task<UnifyNamesPreviewResponse> PreviewUnifyAsync(UnifyNamesPreviewRequest request, CancellationToken ct = default);

    /// <summary>تنفيذ توحيد التسمية N←1 (المدير/المشرف — ينقل القيود ويعطّل المجموعات الممتصة بلا هجرة ملفات).</summary>
    Task<UnifyNamesResponse> UnifyNamesAsync(UnifyNamesRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>قيود مجموعة واحدة — لرئيس القسم (محافظته فقط) ولوحة إدارة الفروع.</summary>
    Task<IReadOnlyList<PublicEntityEntryDto>> ListEntriesByGroupAsync(int groupId, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>اقتراح تعديل فردي من المحامي (يبقى بانتظار المراجعة — لا يزامن النصوص).</summary>
    Task<PublicEntityEntryDto?> ProposeEditAsync(int entryId, ProposeEditRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>معاينة إعادة تسمية هوية أم على مستوى المجموعة (المدير/المشرف — قبل التنفيذ).</summary>
    Task<RenameGroupPreviewResponse> PreviewRenameGroupAsync(RenameGroupPreviewRequest request, CancellationToken ct = default);

    /// <summary>إعادة تسمية هوية أم واحدة على مستوى المجموعة بمرسوم إلزامي (المدير/المشرف).</summary>
    Task<RenameGroupResponse> RenameGroupAsync(RenameGroupRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>معاينة إلغاء عدة هويات أم واستبدالها بهوية جديدة (المدير/المشرف — قبل التنفيذ).</summary>
    Task<AbolishReplacePreviewResponse> PreviewAbolishAndReplaceAsync(AbolishReplacePreviewRequest request, CancellationToken ct = default);

    /// <summary>إلغاء عدة هويات أم واستبدالها بهوية أم جديدة بمرسوم إلزامي (المدير/المشرف).</summary>
    Task<AbolishAndReplaceResponse> AbolishAndReplaceAsync(AbolishAndReplaceRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>معاينة موحدة لأي عملية فرع (تعديل تسمية/دمج/إلغاء/توحيد) قبل الاعتماد — بلا كتابة.</summary>
    Task<BranchActionPreviewResponse> PreviewBranchActionAsync(int groupId, PreviewBranchActionRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>تعديل تسمية فرع ضمن محافظة رئيس القسم (بلا مرسوم) — يزامن لقطات الفروع (S7).</summary>
    Task<RenameBranchResponse> RenameBranchAsync(int groupId, int entryId, RenameBranchRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>دمج فرعين نشطين في نفس الهوية الأم والمحافظة (ضمن نطاق رئيس القسم).</summary>
    Task<MergeBranchesResponse> MergeBranchesAsync(int groupId, MergeBranchesRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>إلغاء فرع: بلا هدف لصفر روابط، أو دمج ضمني مع هدف (S4) — ضمن نطاق رئيس القسم.</summary>
    Task<AbolishBranchResponse> AbolishBranchAsync(int groupId, int entryId, AbolishBranchRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>توحيد تسميات عدة فروع في فرع ناجٍ (اختياريًا مع تصحيح كتابة اسمه) — ضمن نطاق رئيس القسم.</summary>
    Task<UnifyBranchesResponse> UnifyBranchesAsync(int groupId, UnifyBranchesRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>اقتراح تعديل بيانات الجهة الأم من رئيس القسم (بلا أي كتابة على القيد).</summary>
    Task<ParentEditSuggestionDto> SuggestParentEditAsync(int entryId, SuggestParentEditRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>قائمة اقتراحات تعديل الجهة الأم (تبويب الإدارة / حالة المعلّق في نافذة الفروع).</summary>
    Task<PagedResult<ParentEditSuggestionDto>> ListParentEditSuggestionsAsync(ParentEditSuggestionListQuery query, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>قبول/رفض اقتراح تعديل الجهة الأم (المدير/المشرف فقط).</summary>
    Task<ParentEditSuggestionDto?> ReviewParentEditSuggestionAsync(int suggestionId, ReviewParentEditSuggestionRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>سحب ذاتي لاقتراح معلّق (الرئيس المُنشئ نفسه فقط) — يبقى بلا مساس بالقيد.</summary>
    Task<ParentEditSuggestionDto?> WithdrawParentEditSuggestionAsync(int suggestionId, EntityRegistryActor actor, CancellationToken ct = default);
}
