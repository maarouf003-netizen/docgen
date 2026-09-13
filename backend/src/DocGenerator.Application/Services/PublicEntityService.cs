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

/// <summary>
/// خدمة السجل المرجعي للجهات العامة (نموذج الحوكمة الجديد): أي جهة يُدخلها
/// محامٍ تُخزَّن بـ Status=Final لكنها تبقى «بحاجة مراجعة» (NeedsReview=true) فلا
/// تظهر لبوات المندوبين قبل اعتماد/تعديل رئيس قسمها (المواءمة السلوكية §6bis)؛
/// الاعتماد يقفل المراجعة بصمت، والتعديل — وتغيير التسمية تحديدًا — يبلّغ
/// المُدخِل بالاسم القديم والجديد. الإدارة تعدّل كل السجل بتنفيذ فوري.
/// إعادة التسمية الجماعية تزامن الأعمدة النصية ضمن معاملة واحدة (د5)، وأداة
/// الاستيراد التاريخي تعتمد نهائيًا مباشرة (د12).
/// </summary>
public sealed class PublicEntityService : IPublicEntityService
{
    private const string DefaultBranchName = "الجهة الأم";

    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;
    private readonly IHeadAlertRepository _headAlerts;
    private readonly IRepository<PublicEntityChangeEvent> _changeEvents;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IRepository<ParentEditSuggestion> _suggestions;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IUserRepository _users;
    private readonly IAppealRepository _appeals;

    public PublicEntityService(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IHeadAlertRepository headAlerts,
        IRepository<PublicEntityChangeEvent> changeEvents,
        IRepository<DocumentOccurrence> occurrences,
        IRepository<ParentEditSuggestion> suggestions,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IUserRepository users,
        IAppealRepository appeals)
    {
        _entities = entities;
        _branches = branches;
        _headAlerts = headAlerts;
        _changeEvents = changeEvents;
        _occurrences = occurrences;
        _suggestions = suggestions;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _users = users;
        _appeals = appeals;
    }

    // ── القراءة ──

    public async Task<PagedResult<PublicEntityEntryDto>> ListAsync(EntityRegistryListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var qNorm = ArabicNameNormalizer.Normalize(query.Q);
        var governorate = NormalizeOptional(query.Governorate);
        var status = NormalizeOptional(query.Status);
        var branchName = NormalizeOptional(query.BranchName);

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        var entries = groups
            .SelectMany(g => g.Entries.Select(e => (Group: g, Entry: e)))
            .Where(x => query.IncludePending || x.Entry.Status != EntityStatusCatalog.Pending)
            .Where(x => query.IncludeInactive || x.Entry.IsActive)
            .Where(x => governorate is null || x.Entry.IsParentEntity || x.Entry.Governorate == governorate)
            .Where(x => status is null || x.Entry.Status == status)
            .Where(x => branchName is null || x.Entry.IsParentEntity || x.Entry.BranchName == branchName)
            .Where(x => qNorm.Length == 0
                || ArabicNameNormalizer.Normalize(x.Group.CanonicalName).Contains(qNorm)
                || x.Entry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText).Contains(qNorm)))
            .OrderBy(x => x.Group.CanonicalName, StringComparer.Ordinal)
            .ThenBy(x => x.Entry.Governorate, StringComparer.Ordinal)
            .ThenBy(x => x.Entry.BranchName, StringComparer.Ordinal)
            .ToList();

        var result = new PagedResult<PublicEntityEntryDto>
        {
            Page = page,
            PerPage = perPage,
            TotalCount = entries.Count,
        };
        result.Items = entries
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(x => ToEntryDto(x.Group, x.Entry))
            .ToList();
        return result;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<PublicEntityGroupDto>> ListGroupsAsync(EntityGroupListQuery query, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var qNorm = ArabicNameNormalizer.Normalize(query.Q);
        var governorate = NormalizeOptional(query.Governorate);
        var excludeIds = query.ExcludeIds is null
            ? new HashSet<int>()
            : new HashSet<int>(query.ExcludeIds.Distinct());
        // معرّفات يجب ضمان ظهورها في النتيجة مهما كانت ترتيبها/صفحتها
        // (تُستخدم لنافذة توحيد التسمية لضمان تواجد «الهوية الهدف» السابقة الاختيار).
        var includeIds = query.IncludeIds is null
            ? new HashSet<int>()
            : new HashSet<int>(query.IncludeIds.Distinct());

        // نطاق رئيس القسم: محافظة فرعه فقط — بلا فرع/محافظة لا يُعرض شيء
        string? headGovernorate = null;
        bool isHead = actor.Role == UserRole.Head;
        if (isHead)
        {
            if (!actor.BranchId.HasValue)
                return new PagedResult<PublicEntityGroupDto> { Page = page, PerPage = perPage, TotalCount = 0, Items = new List<PublicEntityGroupDto>() };
            var branch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            headGovernorate = NormalizeOptional(branch?.Governorate);
            if (headGovernorate is null)
                return new PagedResult<PublicEntityGroupDto> { Page = page, PerPage = perPage, TotalCount = 0, Items = new List<PublicEntityGroupDto>() };
        }

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);

        var filtered = groups
            .Where(g => g.IsActive)
            .Where(g => !excludeIds.Contains(g.Id))
            .Where(g => !isHead || g.Entries.Any(e => e.IsActive && e.Governorate == headGovernorate))
            .Where(g => governorate is null || g.Entries.Any(e => e.IsActive && e.Governorate == governorate))
            .Where(g => qNorm.Length == 0
                || ArabicNameNormalizer.Normalize(g.CanonicalName).Contains(qNorm)
                || g.Entries.Any(e => e.IsActive && e.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText).Contains(qNorm))))
            .OrderBy(g => g.CanonicalName, StringComparer.Ordinal)
            .ToList();

        var totalCount = filtered.Count;
        var pageItems = filtered
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToList();

        // ضمان تواجد الهويات المطلوبة (IncludeIds) في النتيجة حتى لو كانت خارج نطاق الصفحة
        // الحالية بسبب الفرز/الترقيم — بشرط أن تكون مرّت من نفس الفلاتر (نشطة، نطاق، بحث).
        if (includeIds.Count > 0)
        {
            var included = filtered.Where(g => includeIds.Contains(g.Id)).ToList();
            if (included.Count > 0)
            {
                var presentIds = new HashSet<int>(pageItems.Select(g => g.Id));
                pageItems = pageItems
                    .Concat(included.Where(g => !presentIds.Contains(g.Id)))
                    .OrderBy(g => g.CanonicalName, StringComparer.Ordinal)
                    .ToList();
            }
        }

        var pageGroupIds = pageItems.Select(g => g.Id).ToList();
        var linkedCounts = await _entities.CountLinkedDocumentsByGroupIdsAsync(pageGroupIds, ct);

        var dtos = pageItems.Select(g =>
        {
            var scoped = g.Entries.Where(e => e.IsActive);
            if (isHead) scoped = scoped.Where(e => e.Governorate == headGovernorate);
            var scopedList = scoped.ToList();
            return new PublicEntityGroupDto(
                g.Id,
                g.CanonicalName,
                g.EntityType,
                g.IsActive,
                scopedList.Count,
                scopedList.Select(e => e.Governorate).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                linkedCounts.TryGetValue(g.Id, out var count) ? count : 0);
        }).ToList();

        return new PagedResult<PublicEntityGroupDto>
        {
            Page = page,
            PerPage = perPage,
            TotalCount = totalCount,
            Items = dtos,
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PublicEntityEntryDto>> ListEntriesByGroupAsync(int groupId, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var group = await _entities.GetGroupAsync(groupId, ct)
            ?? throw new ArgumentException("المجموعة غير موجودة");
        if (!group.IsActive) throw new ArgumentException("المجموعة غير نشطة");

        var entries = await _entities.ListEntriesByGroupAsync(groupId, ct);
        var filtered = entries.Where(e => e.IsActive).ToList();

        // نطاق رئيس القسم: محافظته فقط — مع ضمان ظهور قيد «الجهة الأم» دائمًا (F4).
        if (actor.Role == UserRole.Head && actor.BranchId.HasValue)
        {
            var branch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            var gov = NormalizeOptional(branch?.Governorate);
            if (gov is not null)
                filtered = filtered.Where(e => e.IsParentEntity || e.Governorate == gov).ToList();
            else
                filtered = new List<PublicEntity>();
        }

        return filtered
            .OrderBy(e => e.Governorate, StringComparer.Ordinal)
            .ThenBy(e => e.BranchName, StringComparer.Ordinal)
            .Select(e => ToEntryDto(group, e))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<SimilarToResponse> FindSimilarToGroupAsync(int groupId, double threshold, int maxResults, CancellationToken ct = default)
    {
        var target = await _entities.GetGroupAsync(groupId, ct)
            ?? throw new ArgumentException("المجموعة غير موجودة");
        if (!target.IsActive)
            throw new ArgumentException("المجموعة غير نشطة");

        var t = threshold <= 0 ? ArabicNameSimilarity.DefaultSimilarToThreshold : Math.Clamp(threshold, 0, 1);
        var max = maxResults <= 0 ? ArabicNameSimilarity.DefaultMaxSimilarResults : Math.Min(maxResults, 50);
        var groups = await _entities.ListGroupsWithEntriesAsync(ct);

        var ranked = groups
            .Where(g => g.IsActive && g.Id != groupId)
            .Select(g => (Group: g, Sim: ArabicNameSimilarity.Similarity(target.CanonicalName, g.CanonicalName)))
            .Where(x => x.Sim >= t)
            .OrderByDescending(x => x.Sim)
            .ThenBy(x => x.Group.CanonicalName, StringComparer.Ordinal)
            .Take(max)
            .ToList();

        var ids = ranked.Select(r => r.Group.Id).ToList();
        var linkedCounts = await _entities.CountLinkedDocumentsByGroupIdsAsync(ids, ct);

        var items = ranked.Select(r => new SimilarToItemDto(
            r.Group.Id,
            r.Group.CanonicalName,
            r.Group.EntityType,
            r.Group.Entries.Count(e => e.IsActive),
            linkedCounts.TryGetValue(r.Group.Id, out var c) ? c : 0,
            Math.Round(r.Sim, 3))).ToList();

        return new SimilarToResponse(target.Id, target.CanonicalName, items, t);
    }

    /// <inheritdoc/>
    public async Task<PublicEntityEntryDto?> ProposeEditAsync(int entryId, ProposeEditRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        if (actor.Role != UserRole.Lawyer)
            throw new UnauthorizedAccessException("اقتراح التعديل متاح للمحامي فقط");

        var entry = await _entities.GetEntryWithDetailsAsync(entryId, ct);
        if (entry is null) return null;
        var group = entry.Group;

        // حوكمة S1 — منع صريح للجميع: اقتراح المحامي لا يغيِّر وضع «الجهة الأم»
        // (لا ترقية ولا تخفيض) — يبقى العلم على حاله، والإدارة وحدها عبر التعديل المركزي.
        if (request.IsParentEntity == true)
            throw new UnauthorizedAccessException("لا يُغيَّر وضع «الجهة الأم» عبر الاقتراحات — للإدارة فقط");

        string? newCanonical = null;
        if (!string.IsNullOrWhiteSpace(request.CanonicalName)
            && !string.Equals(request.CanonicalName.Trim(), group.CanonicalName, StringComparison.Ordinal))
        {
            newCanonical = Required(request.CanonicalName, "اسم الجهة مطلوب", 200);
            await EnsureCanonicalAvailableAsync(newCanonical, group.Id, ct);
        }

        var newGovernorate = entry.Governorate;
        if (!string.IsNullOrWhiteSpace(request.Governorate))
            newGovernorate = Required(request.Governorate, "المحافظة مطلوبة", 100);

        var newBranchName = entry.BranchName;
        if (!string.IsNullOrWhiteSpace(request.BranchName))
            newBranchName = RequiredWithFallback(request.BranchName, DefaultBranchName, 200);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            group.EntityType = ValidEntityType(request.EntityType);
        if (!string.IsNullOrWhiteSpace(request.CitationFormula))
            entry.CitationFormula = ValidCitationFormula(request.CitationFormula, entry.CitationFormula);
        if (request.CoverageLabel is not null)
            entry.CoverageLabel = ValidateCoverageLabel(request.CoverageLabel);

        await EnsureNoDuplicateEntryAsync(entry.Id, newCanonical ?? group.CanonicalName, newGovernorate, newBranchName, ct);

        var oldCanonical = group.CanonicalName;
        var oldGov = entry.Governorate;
        var oldBranch = entry.BranchName;

        if (newCanonical is not null) group.CanonicalName = newCanonical;
        entry.Governorate = newGovernorate;
        entry.BranchName = newBranchName;

        entry.NeedsReview = true;
        entry.ReviewedAtUtc = null;
        entry.ReviewedById = null;

        return await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                oldCanonical,
                newCanonical = group.CanonicalName,
                oldGovernorate = oldGov,
                newGovernorate,
                oldBranch,
                newBranch = newBranchName,
                coverageLabel = entry.CoverageLabel,
            });

            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = entry.Id,
                GroupId = group.Id,
                ActionKind = ActionKindCatalog.Propose,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);

            // تنبيه رؤساء محافظة القيد — يُدمج اقتراح تعديل الجهة الواحدة قبل الاعتماد
            // في تنبيه واحد بآخر تعديل (لا يُنشأ تنبيه جديد على كل اقتراح متلاحق).
            var heads = await _entities.ListActiveHeadsByGovernorateAsync(entry.Governorate, token);
            if (heads.Count == 0 && actor.BranchId.HasValue)
                heads = await _entities.ListActiveHeadsByBranchAsync(actor.BranchId.Value, token);
            foreach (var head in heads.Where(h => h.BranchId.HasValue))
            {
                var msg = $"المحامي {actor.Name ?? "محامٍ"} اقترح تعديل جهة «{oldCanonical}» → «{group.CanonicalName}» ({entry.Governorate}/{entry.BranchName}) — بانتظار المراجعة";
                await UpsertEditProposalAlertAsync(entry.Id, head.Id, head.BranchId!.Value, actor.UserId, msg, token);
            }
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "propose_public_entity_edit",
                details: $"اقترح تعديل جهة: «{oldCanonical}» → «{group.CanonicalName}» ({entry.Governorate}/{entry.BranchName})", ct: token);

            return ToEntryDto(group, entry);
        }, ct);
    }

    // ── إنشاء قيد نهائي ──

    public async Task<PublicEntityEntryDto> CreateAsync(CreatePublicEntityRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var canonical = Required(request.CanonicalName, "اسم الجهة مطلوب", 200);
        var entityType = ValidEntityType(request.EntityType);
        var governorate = Required(request.Governorate, "المحافظة مطلوبة", 100);
        var branchName = RequiredWithFallback(request.BranchName, DefaultBranchName, 200);
        var citationFormula = ValidCitationFormula(request.CitationFormula, CitationFormulaCatalog.AddToJob);
        var aliases = CleanAliases(request.Aliases, ArabicNameNormalizer.Normalize(canonical));
        var coverageLabel = ValidateCoverageLabel(request.CoverageLabel);

        await EnsureHeadScopeAsync(actor, null, governorate, ct);
        await EnsureNoDuplicateEntryAsync(excludeEntryId: null, canonical, governorate, branchName, ct);

        // حوكمة S1 — منع صريح للجميع: رئيس القسم والمحامي لا ينشئان قيدًا «لجهة أم»
        // صراحةً (المسار المركزي للإدارة)، والاشتقاق الضمني من اسم الفرع الافتراضي
        // مُجمَّد لهما — الإنشاء بالاسم الافتراضي يبقى فرعًا عاديًا (مع NeedsReview للمحامي).
        if (request.IsParentEntity == true && actor.Role is UserRole.Head or UserRole.Lawyer)
            throw new UnauthorizedAccessException("لا يُنشئ قيد «الجهة الأم» إلا الإدارة عبر المسارات المركزية");
        var isParentEntity = request.IsParentEntity
            ?? (branchName == DefaultBranchName && actor.Role is not (UserRole.Head or UserRole.Lawyer));

        PublicEntityGroup group = new();
        var entry = new PublicEntity();
        await _tx.RunAsync(async token =>
        {
            group = await FindOrCreateGroupAsync(canonical, entityType, actor.UserId, token);
            entry.Group = group;
            entry.GroupId = group.Id;
            entry.Governorate = governorate;
            entry.BranchName = branchName;
            entry.IsParentEntity = isParentEntity;
            entry.CitationFormula = citationFormula;
            entry.CoverageLabel = coverageLabel;
            entry.Status = EntityStatusCatalog.Final;
            entry.CreatedById = actor.UserId;
            entry.CreatedAt = DateTime.UtcNow;
            entry.IsActive = true;
            // نموذج الحوكمة الجديد: ما أدخله محامٍ يُخزَّن نهائيًا لكنه يبقى بانتظار
            // مراجعة رئيس القسم فلا يظهر لبوات المندوبين حتى الاعتماد (المواءمة
            // السلوكية §6bis — المستهلك النهائي في PortalRepository يستبعد NeedsReview)؛
            // أما الإدارة/الرئيس فيدخلون مُراجَعًا جاهزًا.
            entry.NeedsReview = actor.Role == UserRole.Lawyer;
            foreach (var alias in aliases)
                entry.Aliases.Add(new PublicEntityAlias { AliasText = alias });

            if (group.Id == 0)
                await _entities.AddGroupAsync(group, token);
            await _entities.AddEntryAsync(entry, token);
            await _uow.SaveChangesAsync(token);

            if (entry.NeedsReview)
                await InsertEntryReviewAlertsAsync(entry, actor.Name, token);

            await _audit.LogAsync(actor.Name, "create_public_entity",
                details: $"أضاف قيد جهة: {canonical} ({governorate} / {branchName})"
                    + (entry.NeedsReview ? " — بانتظار مراجعة رئيس القسم" : string.Empty),
                ct: token);
        }, ct);

        return ToEntryDto(group, entry);
    }

    /// <summary>
    /// تنبيه رئيس فرع المُدخِل (ومحافظة القيد كاحتياط): «المحامي فلان أدخل جهة عامة
    /// جديدة يرجى مراجعتها». نطاق المراجعة الآن هو ما أدخله محامو فرع الرئيس
    /// بغض النظر عن محافظة الجهة، لذا يُوجَّه التنبيه أولًا إلى رؤساء فرع المُدخِل؛
    /// وإن لم يوجد رئيس لفرعه يُحتاط بإرساله إلى رؤساء محافظة القيد.
    /// </summary>
    private async Task InsertEntryReviewAlertsAsync(PublicEntity entry, string? actorName, CancellationToken token)
    {
        var creator = await _entities.GetEntryWithDetailsAsync(entry.Id, token);
        var creatorFullName = creator?.CreatedBy?.FullName ?? actorName ?? "محامٍ";
        var creatorBranchId = creator?.CreatedBy?.BranchId;
        List<User> heads;
        if (creatorBranchId.HasValue)
            heads = await _entities.ListActiveHeadsByBranchAsync(creatorBranchId.Value, token);
        else
            heads = await _entities.ListActiveHeadsByGovernorateAsync(entry.Governorate, token);
        if (heads.Count == 0 && creatorBranchId.HasValue)
            heads = await _entities.ListActiveHeadsByGovernorateAsync(entry.Governorate, token);
        if (heads.Count == 0)
            return;

        var message = $"المحامي {creatorFullName} أدخل جهة عامة جديدة «{entry.Group.CanonicalName}» "
            + $"({entry.Governorate} / {entry.BranchName}) — يرجى مراجعتها";

        foreach (var head in heads)
        {
            var alert = new HeadAlert
            {
                BranchId = head.BranchId!.Value,
                CreatedById = head.Id,
                TargetType = HeadAlertTargetType.Branch,
                Message = message.Length > 2000 ? message[..2000] : message,
                CreatedAt = DateTime.UtcNow,
                Recipients = { new HeadAlertRecipient { UserId = head.Id } },
            };
            await _headAlerts.AddAsync(alert, token);
        }
        await _uow.SaveChangesAsync(token);
    }

    /// <summary>
    /// إدراج — أو دمج — تنبيه اقتراح تعديل لرئيس قسم: إن وُجد تنبيه قائم (غير مقروء)
    /// لنفس القيد والمستلم، يُحدَّث نصّه بآخر تعديل وزمنه بدل إنشاء تنبيه إضافي،
    /// فيبقى لمنتظر المراجعة تنبيه واحد بآخر تعديل بدل تراكم التنبيهات المتلاحقة.
    /// </summary>
    private async Task UpsertEditProposalAlertAsync(int entryId, int headId, int branchId, int actorUserId, string message, CancellationToken token)
    {
        var latest = await _headAlerts.FindLatestPendingByEntityAsync(entryId, headId, token);
        if (latest is not null)
        {
            latest.Message = message.Length > 2000 ? message[..2000] : message;
            latest.CreatedAt = DateTime.UtcNow;
            return;
        }

        var alert = new HeadAlert
        {
            BranchId = branchId,
            CreatedById = actorUserId,
            PublicEntityId = entryId,
            TargetType = HeadAlertTargetType.Branch,
            Message = message.Length > 2000 ? message[..2000] : message,
            CreatedAt = DateTime.UtcNow,
            Recipients = { new HeadAlertRecipient { UserId = headId } },
        };
        await _headAlerts.AddAsync(alert, token);
    }

    // ── تعديل قيد / إعادة تسمية جماعية (د5) ──

    public async Task<PublicEntityEntryDto?> UpdateAsync(int entryId, UpdatePublicEntityRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var entry = await _entities.GetEntryWithDetailsAsync(entryId, ct);
        if (entry is null)
            return null;
        var group = entry.Group;

        string? newCanonical = null;
        if (!string.IsNullOrWhiteSpace(request.CanonicalName)
            && !string.Equals(request.CanonicalName.Trim(), group.CanonicalName, StringComparison.Ordinal))
        {
            newCanonical = Required(request.CanonicalName, "اسم الجهة مطلوب", 200);
            await EnsureCanonicalAvailableAsync(newCanonical, group.Id, ct);
        }

        var newGovernorate = entry.Governorate;
        if (!string.IsNullOrWhiteSpace(request.Governorate))
            newGovernorate = Required(request.Governorate, "المحافظة مطلوبة", 100);

        var newBranchName = entry.BranchName;
        if (!string.IsNullOrWhiteSpace(request.BranchName))
            newBranchName = RequiredWithFallback(request.BranchName, DefaultBranchName, 200);

        // نطاق رئيس القسم: قيود محافظته فقط، ولا يعيد تسمية هوية تشمل محافظات أخرى (د5/د6).
        await EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
        // حارس الجهة الأم (C3/F3): لا يحرّر رئيس القسم قيد «الجهة الأم» إطلاقًا — الاقتراح فقط.
        GuardHeadCannotEditParent(actor, entry);
        if (actor.Role == UserRole.Head)
        {
            if (!string.Equals(newGovernorate, entry.Governorate, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("رئيس القسم مقصور على قيود محافظة فرعه");
            if (newCanonical is not null
                && group.Entries.Any(e => e.Id != entry.Id && e.Governorate != entry.Governorate))
                throw new UnauthorizedAccessException("إعادة تسمية الهوية تشمل قيودًا خارج محافظة فرعك");
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            group.EntityType = ValidEntityType(request.EntityType);
        if (!string.IsNullOrEmpty(request.Status))
        {
            if (!EntityStatusCatalog.IsValid(request.Status))
                throw new ArgumentException("حالة القيد غير صالحة (final/pending)");
            entry.Status = request.Status!;
        }
        if (request.IsActive.HasValue)
            entry.IsActive = request.IsActive.Value;
        if (!string.IsNullOrWhiteSpace(request.CitationFormula))
            entry.CitationFormula = ValidCitationFormula(request.CitationFormula, entry.CitationFormula);
        if (request.CoverageLabel is not null)
            entry.CoverageLabel = ValidateCoverageLabel(request.CoverageLabel);
        if (request.IsParentEntity is bool isParent)
        {
            if (isParent && actor.Role == UserRole.Head)
                throw new UnauthorizedAccessException("رئيس القسم لا يرقّي قيدًا إلى جهة أم — أرسل اقتراح تعديل للإدارة");
            entry.IsParentEntity = isParent;
        }
        else
        {
            // الاشتقاق الضمني من اسم الفرع الافتراضي مُجمَّد للرئيس (حوكمة S1)؛
            // يُحفَظ وضعه الحالي (فرع عادي) بلا رفض.
            entry.IsParentEntity = newBranchName == DefaultBranchName && actor.Role != UserRole.Head;
        }

        await EnsureNoDuplicateEntryAsync(entry.Id, group.CanonicalName, newGovernorate, newBranchName, ct);

        // حقول المرسوم للتعديلات العامة بمرسوم (المدير/المشرف) — تاريخ حر نصه مثال 1/8/2026
        var decreeKind = NormalizeOptional(request.DecreeKind);
        var decreeNumber = NormalizeOptional(request.DecreeNumber);
        var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرسوم");
        if (decreeKind is not null && decreeKind.Length > 100)
            throw new ArgumentException("نوع المرسوم أطول من 100 حرف");
        if (decreeNumber is not null && decreeNumber.Length > 100)
            throw new ArgumentException("رقم المرسوم أطول من 100 حرف");

        var oldCanonical = group.CanonicalName;
        var renamed = newCanonical is not null;
        // حالة المراجعة قبل التعديل: من كان قيد المراجعة يُقفلها أي تعديل مراجِع،
        // وتغيير التسمية خلالها يوجّه إشعارًا للمُدخِل المحامي بالاسمين.
        var wasNeedsReview = entry.NeedsReview;
        var createdByLawyer = entry.CreatedBy?.Role == UserRole.Lawyer;

        if (renamed)
            group.CanonicalName = newCanonical!;
        entry.Governorate = newGovernorate;
        entry.BranchName = newBranchName;
        if (entry.NeedsReview)
        {
            entry.NeedsReview = false;
            entry.ReviewedAtUtc = DateTime.UtcNow;
            entry.ReviewedById = actor.UserId;
        }

        await _tx.RunAsync(async token =>
        {
            var affectedDocs = renamed
                ? await SyncTextsAfterRenameAsync(oldCanonical, newCanonical!, actor.Name, token)
                : new List<Document>();
            var affected = affectedDocs.Count;

            if (renamed && wasNeedsReview && createdByLawyer)
                await InsertRenameNoticeToCreatorAsync(entry, oldCanonical, group.CanonicalName, token);

            // مزامنة لقطات الاستئنافات عند إعادة تسمية قيد معيّن
            if (affectedDocs.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(affectedDocs, actor, token);

            await _uow.SaveChangesAsync(token);

            // سجل التغيير للتعديلات العامة بمرسوم (المرحلة 3) — يُنشأ عند وجود مرسوم أو إعادة تسمية
            var hasDecree = decreeKind is not null || decreeNumber is not null || decreeDate is not null;
            if (hasDecree || renamed)
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    oldCanonical,
                    newCanonical = group.CanonicalName,
                    governorate = entry.Governorate,
                    branchName = entry.BranchName,
                    entityType = group.EntityType,
                    decreeKind,
                    decreeNumber,
                    decreeDate = decreeDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                });
                var changeEvent = new PublicEntityChangeEvent
                {
                    EntryId = entry.Id,
                    GroupId = group.Id,
                    ActionKind = renamed ? ActionKindCatalog.Rename : ActionKindCatalog.Update,
                    DecreeKind = decreeKind,
                    DecreeNumber = decreeNumber,
                    DecreeDate = decreeDate,
                    PayloadJson = payload,
                    ActorUserId = actor.UserId,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                await _changeEvents.AddAsync(changeEvent, token);
                await _uow.SaveChangesAsync(token);
            }

            if (renamed)
            {
                await _audit.LogAsync(actor.Name, "rename_public_entity",
                    details: $"أعاد تسمية الجهة: «{oldCanonical}» إلى «{group.CanonicalName}» — مزامنة {affected} ملفًا", ct: token);
            }
            await _audit.LogAsync(actor.Name, "update_public_entity",
                details: $"عدّل قيد الجهة: {group.CanonicalName} ({entry.Governorate} / {entry.BranchName})", ct: token);
        }, ct);

        return ToEntryDto(group, entry);
    }

    /// <summary>
    /// إبلاغ المُدخِل المحامي بتغيير تسمية جهته أثناء المراجعة:
    /// «تم تعديل اسم الجهة التي أدخلتها من “القديم” إلى “الجديد”».
    /// </summary>
    private async Task InsertRenameNoticeToCreatorAsync(PublicEntity entry, string oldName, string newName, CancellationToken token)
    {
        var creator = entry.CreatedBy;
        if (creator is null || creator.BranchId is null)
            return;

        var message = $"تم تعديل اسم الجهة العامة التي أدخلتها من «{oldName}» إلى «{newName}»";
        var alert = new HeadAlert
        {
            BranchId = creator.BranchId.Value,
            CreatedById = entry.ReviewedById ?? creator.Id,
            TargetType = HeadAlertTargetType.Lawyer,
            TargetLawyerId = creator.Id,
            Message = message.Length > 2000 ? message[..2000] : message,
            CreatedAt = DateTime.UtcNow,
            Recipients = { new HeadAlertRecipient { UserId = creator.Id } },
        };
        await _headAlerts.AddAsync(alert, token);
    }

    public async Task<PublicEntityEntryDto?> AddAliasAsync(int entryId, AddPublicEntityAliasRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var entry = await _entities.GetEntryWithDetailsAsync(entryId, ct);
        if (entry is null)
            return null;

        await EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);

        var aliasText = Required(request.AliasText, "الاسم البديل مطلوب", 500);
        var aliasNorm = ArabicNameNormalizer.Normalize(aliasText);
        var canonicalNorm = ArabicNameNormalizer.Normalize(entry.Group.CanonicalName);
        if (aliasNorm == canonicalNorm
            || entry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == aliasNorm))
            throw new ArgumentException("الاسم البديل مستخدم مسبقًا لهذه الجهة");

        await _tx.RunAsync(async token =>
        {
            entry.Aliases.Add(new PublicEntityAlias { AliasText = aliasText });
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "add_public_entity_alias",
                details: $"أضاف اسمًا بديلًا للجهة {entry.Group.CanonicalName}: {aliasText}", ct: token);
        }, ct);

        return ToEntryDto(entry.Group, entry);
    }

    // ── مراجعة سجل الجهات العامة الممثلة (النموذج الجديد) ──

    /// <summary>
    /// قائمة «بحاجة مراجعة»: رئيس القسم يرى ما أدخله محامو فرعه (بغض النظر عن محافظة
    /// الجهة نفسها — قد يُقيم محامٍ ملفًا تنفيذيًا على جهة تتبع محافظة أخرى)، والمدير/
    /// المشرف يرىان كل السجل. رئيس بلا فرع مضبوط تعني قائمة فارغة.
    /// </summary>
    public async Task<List<PublicEntityEntryDto>> ListNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default)
    {
        int? headBranchId = null;
        if (actor.Role == UserRole.Head)
        {
            var branch = actor.BranchId is null ? null : await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            headBranchId = branch?.Id;
            if (headBranchId is null)
                return new List<PublicEntityEntryDto>();
        }

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        return groups
            .SelectMany(g => g.Entries.Select(e => (Group: g, Entry: e)))
            .Where(x => x.Entry.NeedsReview)
            .Where(x => headBranchId is null
                || (x.Entry.CreatedBy != null && x.Entry.CreatedBy.BranchId == headBranchId))
            .OrderByDescending(x => x.Entry.CreatedAt)
            .Select(x => ToEntryDto(x.Group, x.Entry))
            .ToList();
    }

    // ── سجل تغييرات الجهات (د5 §7) ──

    private static (DateTime? From, DateTime? To) ParseChangeEventPeriod(string? fromRaw, string? toRaw)
    {
        DateTime? from = null, to = null;
        var f = ActionDateParser.TryParse(fromRaw);
        if (f.HasValue) from = f.Value.Date;
        var t = ActionDateParser.TryParse(toRaw);
        if (t.HasValue) to = t.Value.Date.AddDays(1).AddTicks(-1);
        return (from, to);
    }

    private static bool MatchesGovernorate(PublicEntityChangeEvent e, string? governorate)
    {
        if (governorate is null) return true;
        // حدث مستوى قيد: نطاقه محافظة القيد نفسه — لا تتسرّب أحداث محافظة أخرى لرؤساء
        // محافظات أعضاء المجموعة (بعد ThenInclude(g => g.Entries) في جلب الأحداث).
        if (e.Entry != null) return e.Entry.Governorate == governorate;
        // حدث مستوى مجموعة (بلا EntryId — عمليات مركزية بمرسوم): يظهر لرؤساء محافظات أعضائها.
        if (e.Group != null) return e.Group.Entries.Any(en => en.Governorate == governorate);
        return false;
    }

    private async Task<List<PublicEntityChangeEvent>> GetFilteredChangeEventsAsync(
        EntityChangeEventQuery query,
        EntityRegistryActor actor,
        CancellationToken ct)
    {
        var all = await _entities.ListChangeEventsAsync(ct);
        // نطاق رئيس القسم: محافظته فقط (جبر خادمي يتجاهل پارامتر العميل تمامًا).
        string? governorate = NormalizeOptional(query.Governorate);
        if (actor.Role == UserRole.Head && actor.BranchId.HasValue)
        {
            var headBranch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            governorate = NormalizeOptional(headBranch?.Governorate);
        }
        var actionKind = NormalizeOptional(query.ActionKind);
        var (from, to) = ParseChangeEventPeriod(query.From, query.To);
        return all
            .Where(e => MatchesGovernorate(e, governorate))
            .Where(e => actionKind is null || e.ActionKind == actionKind)
            .Where(e => query.ActorUserId is null || e.ActorUserId == query.ActorUserId)
            .Where(e => from is null || e.CreatedAtUtc >= from)
            .Where(e => to is null || e.CreatedAtUtc <= to)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToList();
    }

    public async Task<PagedResult<EntityChangeEventDto>> ListChangeEventsAsync(EntityChangeEventQuery query, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var filtered = await GetFilteredChangeEventsAsync(query, actor, ct);
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var total = filtered.Count;
        var items = filtered.Skip((page - 1) * perPage).Take(perPage).Select(ToChangeEventDto).ToList();
        return new PagedResult<EntityChangeEventDto> { Items = items, Page = page, PerPage = perPage, TotalCount = total };
    }

    public async Task<byte[]> ExportChangeEventsAsync(EntityChangeEventQuery query, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var filtered = await GetFilteredChangeEventsAsync(query, actor, ct);
        var items = filtered.Take(5000).Select(ToChangeEventDto).ToList();
        await _audit.LogAsync(actor.Name, "export_change_events",
            details: $"تصدير سجل تغييرات الجهات: {items.Count} سطرًا" + (query.Governorate != null ? $" محافظة={query.Governorate}" : ""), ct: ct);
        var exporter = new ExcelExportService();
        return exporter.BuildChangeEventsWorkbook(items);
    }

    private static EntityChangeEventDto ToChangeEventDto(PublicEntityChangeEvent e) => new(
        e.Id,
        e.EntryId,
        e.GroupId,
        e.ActionKind,
        e.DecreeKind,
        e.DecreeNumber,
        e.DecreeDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        e.PayloadJson,
        e.ActorUserId,
        e.ActorUser?.FullName ?? e.ActorUser?.Username,
        e.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
        e.Entry?.Governorate ?? e.Group?.Entries.FirstOrDefault()?.Governorate,
        e.Group?.CanonicalName ?? e.Entry?.Group?.CanonicalName);

    /// <summary>اعتماد قيد كما هو: يقفل المراجعة دون تعديل ودون إشعار للمُدخِل (حسب القرار).</summary>
    public async Task<PublicEntityEntryDto?> ApproveReviewAsync(int entryId, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var entry = await _entities.GetEntryWithDetailsAsync(entryId, ct);
        if (entry is null)
            return null;

        await EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);

        await _tx.RunAsync(async token =>
        {
            entry.NeedsReview = false;
            entry.ReviewedAtUtc = DateTime.UtcNow;
            entry.ReviewedById = actor.UserId;
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "approve_entity_review",
                details: $"اعتمد مراجعة قيد الجهة: {entry.Group.CanonicalName} ({entry.Governorate} / {entry.BranchName})", ct: token);
        }, ct);

        return ToEntryDto(entry.Group, entry);
    }

    // ── الاستيراد التاريخي (د12) ──

    public async Task<ImportPreviewResponse> PreviewImportAsync(CancellationToken ct = default)
        => new(DateTime.UtcNow, await BuildImportCandidatesAsync(ct));

    public async Task<ImportCommitResultDto> CommitImportAsync(ImportCommitRequest request, int actorUserId, string? actorName, CancellationToken ct = default)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("لم تُحدَّد نصوص للاستيراد");

        // مرجعية الخادم: الكتابات البديلة تؤخذ من معاينة حية لا من طلب العميل.
        var candidates = (await BuildImportCandidatesAsync(ct))
            .ToDictionary(i => i.NormalizedName, StringComparer.Ordinal);

        int groupsCreated = 0, entriesCreated = 0, aliasesAdded = 0, skipped = 0;
        var knownGroups = new Dictionary<string, PublicEntityGroup>(StringComparer.Ordinal);

        await _tx.RunAsync(async token =>
        {
            foreach (var item in request.Items)
            {
                var canonical = Required(item.CanonicalName, "اسم الجهة مطلوب", 200);
                var entityType = ValidEntityType(item.EntityType);
                var governorate = Required(item.Governorate, "المحافظة مطلوبة", 100);
                var branchName = RequiredWithFallback(item.BranchName, DefaultBranchName, 200);
                var citationFormula = ValidCitationFormula(item.CitationFormula, CitationFormulaCatalog.AddToJob);

                // فحص التكرار قبل اشتراط المعاينة: إعادة اعتماد بند مستورد سابقًا تتجاهله بهدوء.
                var canonicalNorm = ArabicNameNormalizer.Normalize(canonical);
                if (!knownGroups.TryGetValue(canonicalNorm, out var group))
                    group = await FindGroupByNormAsync(canonicalNorm, token);
                if (group is not null
                    && await _entities.EntryExistsAsync(group.Id, governorate, branchName, token))
                {
                    skipped++;
                    continue;
                }

                // مرجعية الخادم: الكتابات البديلة تؤخذ من معاينة حية لا من طلب العميل.
                if (!candidates.TryGetValue(item.NormalizedName, out var candidate))
                    throw new ArgumentException($"النص غير موجود في المعاينة الحالية: {item.NormalizedName}");

                if (group is null)
                {
                    group = new PublicEntityGroup
                    {
                        CanonicalName = canonical,
                        EntityType = entityType,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                    };
                    await _entities.AddGroupAsync(group, token);
                    groupsCreated++;
                }
                knownGroups[canonicalNorm] = group;

                var entry = new PublicEntity
                {
                    Group = group,
                    Governorate = governorate,
                    BranchName = branchName,
                    CitationFormula = citationFormula,
                    Status = EntityStatusCatalog.Final,
                    CreatedById = actorUserId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                };
                if (item.AddVariantsAsAliases)
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal) { canonicalNorm };
                    foreach (var variant in candidate.Variants)
                    {
                        var vNorm = ArabicNameNormalizer.Normalize(variant.Text);
                        if (vNorm.Length == 0 || !seen.Add(vNorm))
                            continue;
                        entry.Aliases.Add(new PublicEntityAlias { AliasText = variant.Text });
                        aliasesAdded++;
                    }
                }
                await _entities.AddEntryAsync(entry, token);
                entriesCreated++;
            }

            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "import_entity_registry",
                details: $"استورد نصوصًا تاريخية: {entriesCreated} قيدًا نهائيًا ({groupsCreated} هوية، {aliasesAdded} اسمًا بديلًا، تجاهل {skipped})", ct: token);
        }, ct);

        return new ImportCommitResultDto(groupsCreated, entriesCreated, aliasesAdded);
    }

    /// <summary>يجمع النصوص المتمايزة من الطرفين بعد التطبيع مع عدّاداتها، ويستبقي المسجل مسبقًا.</summary>
    private async Task<List<ImportPreviewItemDto>> BuildImportCandidatesAsync(CancellationToken ct)
    {
        var applicantTexts = await _entities.ListDistinctApplicantTextsAsync(ct);
        var executedTexts = await _entities.ListDistinctExecutedTextsAsync(ct);
        var groups = await _entities.ListGroupsWithEntriesAsync(ct);

        var registeredNorms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in groups)
        {
            registeredNorms.Add(ArabicNameNormalizer.Normalize(g.CanonicalName));
            foreach (var e in g.Entries)
                foreach (var a in e.Aliases)
                    registeredNorms.Add(ArabicNameNormalizer.Normalize(a.AliasText));
        }

        var candidates = new Dictionary<string, List<ImportVariantDto>>(StringComparer.Ordinal);
        void Collect(IEnumerable<(string Text, string? Governorate, int DocumentCount)> rows, string side)
        {
            foreach (var row in rows)
            {
                var norm = ArabicNameNormalizer.Normalize(row.Text);
                if (norm.Length == 0 || registeredNorms.Contains(norm))
                    continue;
                if (!candidates.TryGetValue(norm, out var variants))
                    variants = candidates[norm] = new List<ImportVariantDto>();
                variants.Add(new ImportVariantDto(row.Text.Trim(), side, NormalizeOptional(row.Governorate), row.DocumentCount));
            }
        }
        Collect(applicantTexts, "applicant");
        Collect(executedTexts, "executed");

        var items = new List<ImportPreviewItemDto>();
        foreach (var (norm, variants) in candidates)
        {
            var suggested = variants
                .OrderByDescending(v => v.DocumentCount)
                .ThenBy(v => v.Text, StringComparer.Ordinal)
                .First();
            var governorates = variants
                .Where(v => !string.IsNullOrWhiteSpace(v.Governorate))
                .GroupBy(v => v.Governorate!, StringComparer.Ordinal)
                .OrderByDescending(g => g.Sum(v => v.DocumentCount))
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key)
                .ToList();
            items.Add(new ImportPreviewItemDto(
                norm,
                suggested.Text,
                variants.Sum(v => v.DocumentCount),
                governorates,
                variants.OrderByDescending(v => v.DocumentCount).ThenBy(v => v.Text, StringComparer.Ordinal).ToList()));
        }
        return items
            .OrderByDescending(i => i.TotalDocuments)
            .ThenBy(i => i.SuggestedCanonicalName, StringComparer.Ordinal)
            .ToList();
    }

    // ── مزامنة الأعمدة النصية عند إعادة التسمية (شرط ثابت د5) ──

    /// <summary>
    /// يُحدّث صفوف الطرفين المطابقة للاسم القديم (بعد التطبيع) إلى الاسم المعتمد الجديد،
    /// ويُعيد بناء نص طالب التنفيذ ونص البحث لكل ملف متأثر، ثم يُدوّن قبل/بعد كل ملف
    /// في سجل تعديلات الحقول. تعمل داخل معاملة المتصل وتعيد الملفات المتأثرة كأشياء كاملة
    /// (لتمكين مزامنة لقطات الاستئنافات من نفس المجموعة بعد تحرير أسماء صفوفها).
    /// </summary>
    private async Task<List<Document>> SyncTextsAfterRenameAsync(string oldCanonical, string newCanonical, string? actorName, CancellationToken token)
    {
        var oldNorm = ArabicNameNormalizer.Normalize(oldCanonical);
        var newNorm = ArabicNameNormalizer.Normalize(newCanonical);
        if (oldNorm.Length == 0 || oldNorm == newNorm)
            return new List<Document>();

        var logs = new Dictionary<int, List<DocumentFieldChange>>();
        var affectedDocs = new Dictionary<int, Document>();
        void AddLog(int documentId, string fieldKey, string fieldLabel, string? oldValue, string? newValue)
        {
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                return;
            if (!logs.TryGetValue(documentId, out var list))
                logs[documentId] = list = new List<DocumentFieldChange>();
            list.Add(new DocumentFieldChange
            {
                DocumentId = documentId,
                FieldKey = fieldKey,
                FieldLabel = fieldLabel,
                OldValue = Clamp(oldValue),
                NewValue = Clamp(newValue),
            });
        }

        var applicantNames = (await _entities.ListDistinctApplicantTextsAsync(token))
            .Select(t => t.Name)
            .Where(n => ArabicNameNormalizer.Normalize(n) == oldNorm)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (applicantNames.Count > 0)
        {
            var rows = await _entities.ListApplicantRowsByNamesAsync(applicantNames, token);
            var docs = rows.Select(r => r.Document).GroupBy(d => d.Id).Select(g => g.First()).ToList();
            var oldTexts = docs.ToDictionary(d => d.Id, d => d.Applicant);

            foreach (var row in rows)
                row.Name = newCanonical;
            foreach (var doc in docs)
            {
                var rebuilt = ApplicantTextBuilder.Build(doc.ApplicantPublicEntities);
                if (!string.IsNullOrWhiteSpace(rebuilt) || string.IsNullOrWhiteSpace(doc.Applicant))
                    doc.Applicant = rebuilt;
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
                affectedDocs[doc.Id] = doc;
                AddLog(doc.Id, nameof(Document.Applicant), "طالب التنفيذ",
                    oldTexts.GetValueOrDefault(doc.Id), doc.Applicant);
            }
        }

        var executedNames = (await _entities.ListDistinctExecutedTextsAsync(token))
            .Select(t => t.EntityName)
            .Where(n => ArabicNameNormalizer.Normalize(n) == oldNorm)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (executedNames.Count > 0)
        {
            var rows = await _entities.ListExecutedRowsByNamesAsync(executedNames, token);
            foreach (var row in rows)
            {
                var oldSignature = JoinNameBranch(row.EntityName, row.EntityBranch);
                row.EntityName = newCanonical;
                AddLog(row.DocumentId, "__Col_ExecutedPublicEntities", "الجهات العامة المنفذ عليها",
                    oldSignature, JoinNameBranch(row.EntityName, row.EntityBranch));
            }

            // إعادة بناء نص البحث مرة واحدة لكل ملف متأثر (لا لكل صف مطابق).
            var executedDocs = rows.Select(r => r.Document).GroupBy(d => d.Id).Select(g => g.First());
            foreach (var doc in executedDocs)
            {
                affectedDocs[doc.Id] = doc;
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
            }
        }

        // طالبو التنفيذ الاعتباريون المربوطون جهة عامة (RegistryId != null): يُعاد
        // تسمية صفوفهم كبقية الجهات — لا يُلمس natural (بلا RegistryId) إطلاقًا.
        var executionApplicantNames = (await _entities.ListDistinctExecutionApplicantTextsAsync(token))
            .Select(t => t.Name)
            .Where(n => ArabicNameNormalizer.Normalize(n) == oldNorm)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (executionApplicantNames.Count > 0)
        {
            var rows = await _entities.ListExecutionApplicantRowsByNamesAsync(executionApplicantNames, token);
            foreach (var row in rows)
            {
                row.Name = newCanonical;
                AddLog(row.DocumentId, "__Col_ExecutionApplicants", "طالبو التنفيذ",
                    Clamp(oldCanonical), Clamp(newCanonical));
            }

            var applicantDocs = rows.Select(r => r.Document).GroupBy(d => d.Id).Select(g => g.First());
            foreach (var doc in applicantDocs)
            {
                affectedDocs[doc.Id] = doc;
                // ملف «منفذ عليه»/«عرض وايداع» بلا جهة طالبة كلاسية: اسم الطالب يُشتق من
                // طلبات التنفيذ الاعتباريين المربوطين جهة عامة فيتطابق العنوان مع الاسم
                // المعياري بعد إعادة التسمية (لا يبقى الاسم القديم في نص البحث).
                if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide)
                    && doc.ApplicantPublicEntities.Count == 0)
                {
                    var executedApplicantName = doc.ExecutionApplicants
                        .Select(a => (a.Name ?? string.Empty).Trim())
                        .FirstOrDefault(v => v.Length > 0);
                    doc.Applicant = executedApplicantName ?? doc.Applicant;
                }
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
            }
        }

        await _uow.SaveChangesAsync(token);
        var details = $"مزامنة إعادة تسمية الجهة: «{oldCanonical}» ← «{newCanonical}»";
        foreach (var (documentId, changes) in logs)
            await _audit.LogDocumentChangeAsync(actorName, "rename_public_entity_sync",
                documentId, documentType: null, details, changes, token);
        return affectedDocs.Values.ToList();
    }

    // ── عمليات فروع رئيس القسم (ضمن محافظته — بلا مرسوم) ──

    /// <inheritdoc/>
    public async Task<BranchActionPreviewResponse> PreviewBranchActionAsync(
        int groupId,
        PreviewBranchActionRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        var group = await _entities.GetGroupAsync(groupId, ct)
            ?? throw new ArgumentException("المجموعة غير موجودة");
        if (!group.IsActive)
            throw new ArgumentException("المجموعة غير نشطة");

        var errors = new List<string>();
        var warnings = new List<string>();
        var previewEntries = new List<BranchPreviewEntryDto>();
        int totalAffected = 0;
        var targetBranch = string.Empty;
        string? summary = null;

        switch (request.Action)
        {
            case ActionKindCatalog.Rename:
            {
                var entry = await _entities.GetEntryWithDetailsAsync(request.EntryId, ct);
                if (entry is null || entry.GroupId != groupId)
                {
                    errors.Add("القيد غير موجود في المجموعة");
                    break;
                }
                if (!entry.IsActive) { errors.Add("القيد غير نشط"); break; }
                try
                {
                    GuardNotParentEntry(entry);
                    GuardHeadCannotEditParent(actor, entry);
                    await EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
                    var newBranch = Required(request.NewBranchName, "اسم الفرع مطلوب", 200);
                    if (string.Equals(newBranch, entry.BranchName, StringComparison.Ordinal))
                        errors.Add("الاسم الجديد مطابق للاسم الحالي");
                    else
                        await EnsureNoDuplicateEntryAsync(entry.Id, entry.Group.CanonicalName, entry.Governorate, newBranch, ct);
                    var count = (await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, ct)).Count;
                    targetBranch = newBranch;
                    totalAffected = count;
                    previewEntries.Add(new BranchPreviewEntryDto(entry.Id, entry.BranchName, entry.Governorate, count));
                    summary = $"إعادة تسمية فرع «{entry.BranchName}» إلى «{newBranch}» — {count} ملفًا متأثرًا";
                }
                catch (ArgumentException ex) { errors.Add(ex.Message); break; }
                break;
            }
            case ActionKindCatalog.Merge:
            {
                if (!request.TargetId.HasValue) { errors.Add("يجب تحديد الفرع الهدف"); break; }
                var source = await _entities.GetEntryWithDetailsAsync(request.EntryId, ct);
                var target = await _entities.GetEntryWithDetailsAsync(request.TargetId.Value, ct);
                try
                {
                    ValidateMergeablePair(groupId, source, target, request.TargetId.Value, actor, ct, errors, warnings);
                }
                catch (ArgumentException ex) { errors.Add(ex.Message); break; }
                if (errors.Count == 0)
                {
                    var count = (await _entities.ListDocumentsLinkedToEntryAsync(request.EntryId, ct)).Count;
                    targetBranch = target!.BranchName;
                    totalAffected = count;
                    previewEntries.Add(new BranchPreviewEntryDto(source!.Id, source.BranchName, source.Governorate,
                        (await _entities.ListDocumentsLinkedToEntryAsync(source.Id, ct)).Count));
                    previewEntries.Add(new BranchPreviewEntryDto(target.Id, target.BranchName, target.Governorate,
                        (await _entities.ListDocumentsLinkedToEntryAsync(target.Id, ct)).Count));
                    summary = $"دمج فرع «{source.BranchName}» في «{target.BranchName}» — {count} ملفًا متأثرًا";
                }
                break;
            }
            case ActionKindCatalog.Abolish:
            {
                var entry = await _entities.GetEntryWithDetailsAsync(request.EntryId, ct);
                if (entry is null || entry.GroupId != groupId) { errors.Add("القيد غير موجود في المجموعة"); break; }
                if (!entry.IsActive) { errors.Add("القيد غير نشط"); break; }
                try
                {
                    GuardNotParentEntry(entry);
                    GuardHeadCannotEditParent(actor, entry);
                    await EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
                    var count = (await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, ct)).Count;
                    PublicEntity? target = request.TargetId.HasValue
                        ? await _entities.GetEntryWithDetailsAsync(request.TargetId.Value, ct)
                        : null;
                    if (count > 0 && target is null)
                    {
                        errors.Add("الفرع مرتبط بملفات ولا يمكن إلغاؤه دون فرع هدف بديل (S4)");
                    }
                    else if (target is not null)
                    {
                        ValidateMergeablePair(groupId, entry, target, request.TargetId!.Value, actor, ct, errors, warnings);
                    }
                    targetBranch = target?.BranchName ?? entry.BranchName;
                    totalAffected = count;
                    previewEntries.Add(new BranchPreviewEntryDto(entry.Id, entry.BranchName, entry.Governorate, count));
                    summary = count == 0
                        ? $"إلغاء فرع «{entry.BranchName}» بلا ملفات مرتبطة (تعطيل مباشر)"
                        : $"إلغاء فرع «{entry.BranchName}» بدمجه في «{targetBranch}» — {count} ملفًا متأثرًا";
                }
                catch (ArgumentException ex) { errors.Add(ex.Message); break; }
                break;
            }
            case ActionKindCatalog.Unify:
            {
                var target = await _entities.GetEntryWithDetailsAsync(request.EntryId, ct);
                if (target is null || target.GroupId != groupId) { errors.Add("الفرع الهدف غير موجود في المجموعة"); break; }
                var absorbed = request.AbsorbedIds?.Where(x => x != target.Id).Distinct().ToList() ?? new List<int>();
                if (absorbed.Count == 0) { errors.Add("لا توجد فروع محددة للتوحيد (فرع واحد على الأقل غير الهدف)"); break; }
                try
                {
                    GuardNotParentEntry(target);
                    GuardHeadCannotEditParent(actor, target);
                    await EnsureHeadScopeAsync(actor, target, target.Governorate, ct);
                    var absorbedDocIds = new Dictionary<int, Document>();
                    foreach (var absorbedId in absorbed)
                    {
                        var ae = await _entities.GetEntryWithDetailsAsync(absorbedId, ct);
                        ValidateMergeablePair(groupId, ae, target, target.Id, actor, ct, errors, warnings);
                        if (errors.Count > 0) break;
                        foreach (var doc in await _entities.ListDocumentsLinkedToEntryAsync(ae!.Id, ct))
                            absorbedDocIds[doc.Id] = doc;
                        previewEntries.Add(new BranchPreviewEntryDto(ae.Id, ae.BranchName, ae.Governorate,
                            (await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, ct)).Count));
                    }
                    if (errors.Count == 0)
                    {
                        string? finalBranch = null;
                        if (!string.IsNullOrWhiteSpace(request.CorrectedName)
                            && !string.Equals(request.CorrectedName.Trim(), target.BranchName, StringComparison.Ordinal))
                        {
                            var corrected = Required(request.CorrectedName, "اسم الفرع مطلوب", 200);
                            try
                            {
                                await EnsureNoDuplicateEntryAsync(target.Id, target.Group.CanonicalName, target.Governorate, corrected, ct);
                            }
                            catch (ArgumentException ex) { errors.Add(ex.Message); }
                            finalBranch = corrected;
                            foreach (var doc in await _entities.ListDocumentsLinkedToEntryAsync(target.Id, ct))
                                absorbedDocIds[doc.Id] = doc;
                        }
                        targetBranch = finalBranch ?? target.BranchName;
                        totalAffected = absorbedDocIds.Count;
                        summary = $"توحيد {absorbed.Count} فرعًا في «{targetBranch}» — {totalAffected} ملفًا متأثرًا";
                    }
                }
                catch (ArgumentException ex) { errors.Add(ex.Message); }
                break;
            }
            default:
                throw new ArgumentException("إجراء غير صالح: rename/merge/abolish/unify");
        }

        return new BranchActionPreviewResponse(
            request.Action,
            summary ?? string.Empty,
            targetBranch,
            previewEntries,
            totalAffected,
            warnings,
            errors);
    }

    /// <summary>
    /// تحقق شروط الدمج/الطيّ القاسية بين قيدين (المجموعة + المحافظة + النشاط + نطاق
    /// رئيس القسم + بلا NeedsReview). يملأ الأخطاء/التحذيرات بلا رمي (للشروط) أو يرمي
    /// UnauthorizedAccessException (للنطاق/حارس الأم).
    /// </summary>
    private void ValidateMergeablePair(
        int groupId,
        PublicEntity? source,
        PublicEntity? target,
        int targetId,
        EntityRegistryActor actor,
        CancellationToken ct,
        List<string> errors,
        List<string> warnings)
    {
        if (source is null || target is null) { errors.Add("الفرع غير موجود"); return; }
        if (source.GroupId != groupId || target.GroupId != groupId) { errors.Add("الفروع المحددة ليست ضمن نفس المجموعة"); return; }
        if (source.Id == targetId) { errors.Add("لا يمكن دمج الفرع مع نفسه"); return; }
        if (!source.IsActive || !target.IsActive) { errors.Add("أحد الفروع غير نشط"); return; }
        if (source.NeedsReview || target.NeedsReview) { errors.Add("لا يمكن الدمج لوجود قيد بانتظار المراجعة"); return; }
        if (!string.Equals(source.Governorate, target.Governorate, StringComparison.Ordinal))
        { errors.Add("المحافظتان مختلفتان — الدمج يتطلب نفس المحافظة"); return; }
        GuardNotParentEntry(source);
        GuardNotParentEntry(target);
        GuardHeadCannotEditParent(actor, source);
        GuardHeadCannotEditParent(actor, target);
        // النطاق يُرمى كـ Unauthorized — لا يُعرض كخطأ قابل للتجاوز في المعاينة.
        EnsureHeadScopeAsync(actor, source, source.Governorate, ct).GetAwaiter().GetResult();
        EnsureHeadScopeAsync(actor, target, target.Governorate, ct).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<RenameBranchResponse> RenameBranchAsync(
        int groupId,
        int entryId,
        RenameBranchRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        var entry = await GetActiveGroupEntryAsync(groupId, entryId, actor, ct);
        var oldBranch = entry.BranchName;
        var newBranch = Required(request.NewBranchName, "اسم الفرع مطلوب", 200);
        if (string.Equals(oldBranch, newBranch, StringComparison.Ordinal))
            throw new ArgumentException("الاسم الجديد مطابق للاسم الحالي");
        await EnsureNoDuplicateEntryAsync(entry.Id, entry.Group.CanonicalName, entry.Governorate, newBranch, ct);

        var oldCanonical = entry.Group.CanonicalName;
        var wasNeedsReview = entry.NeedsReview;
        var createdByLawyer = entry.CreatedBy?.Role == UserRole.Lawyer;

        entry.BranchName = newBranch;
        if (request.CoverageLabel is not null)
            entry.CoverageLabel = ValidateCoverageLabel(request.CoverageLabel);
        // إعادة التسمية تُقفل أي مراجعة معلّقة (نفس سلوك Update:636).
        if (entry.NeedsReview)
        {
            entry.NeedsReview = false;
            entry.ReviewedAtUtc = DateTime.UtcNow;
            entry.ReviewedById = actor.UserId;
        }

        int affected = 0;
        int changeEventId = 0;
        await _tx.RunAsync(async token =>
        {
            var affectedDocs = await SyncBranchLabelsAsync(entry, newBranch, token);
            if (affectedDocs.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(affectedDocs, actor, token);
            affected = affectedDocs.Count;

            var aliasesAdded = 0;
            AddEachExtraAlias(entry, FullEntryName(oldCanonical, entry.Governorate, oldBranch), ref aliasesAdded);
            await _uow.SaveChangesAsync(token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Rename,
                groupId,
                entryId,
                oldBranchName = oldBranch,
                newBranchName = newBranch,
                governorate = entry.Governorate,
                coverageLabel = entry.CoverageLabel,
                oldCanonical,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = entry.Id,
                GroupId = groupId,
                ActionKind = ActionKindCatalog.Rename,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);
            changeEventId = changeEvent.Id;

            await InsertBranchOccurrencesAsync(affectedDocs,
                $"تم تغيير اسم فرع «{oldCanonical}» من «{oldBranch}» إلى «{newBranch}»", actor, token);
            await InsertBranchChangeAlertAsync(entry,
                $"تم تغيير اسم فرع جهة «{oldCanonical}» ({entry.Governorate}): من «{oldBranch}» إلى «{newBranch}»", actor, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "rename_branch",
                details: $"أعاد تسمية فرع: «{oldCanonical}» ({entry.Governorate}/{oldBranch}) → «{newBranch}» — مزامنة {affected} ملفًا", ct: token);
            if (wasNeedsReview && createdByLawyer)
                await InsertRenameNoticeToCreatorAsync(entry, oldCanonical, entry.Group.CanonicalName, token);
        }, ct);

        return new RenameBranchResponse(entry.Id, oldBranch, newBranch, affected, changeEventId);
    }

    /// <inheritdoc/>
    public async Task<MergeBranchesResponse> MergeBranchesAsync(
        int groupId,
        MergeBranchesRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        if (request.SourceEntryId == request.TargetEntryId)
            throw new ArgumentException("لا يمكن دمج الفرع مع نفسه");
        var source = await GetActiveGroupEntryAsync(groupId, request.SourceEntryId, actor, ct);
        var target = await GetActiveGroupEntryAsync(groupId, request.TargetEntryId, actor, ct);
        if (!string.Equals(source.Governorate, target.Governorate, StringComparison.Ordinal))
            throw new ArgumentException("المحافظتان مختلفتان — الدمج يتطلب نفس المحافظة");
        if (source.NeedsReview || target.NeedsReview)
            throw new ArgumentException("لا يمكن الدمج لوجود قيد بانتظار المراجعة");

        int affected = 0;
        int changeEventId = 0;
        await _tx.RunAsync(async token =>
        {
            var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(source.Id, token);
            foreach (var doc in linkedDocs)
            {
                RepointEntryLinks(doc, source.Id, target.Id);
                // بعد إعادة التوجيه تصبح صفوف المصدر صفوفًا للهدف: لقطة الفرع عليها تتبدل إلى فرع الهدف (S7).
                foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == target.Id))
                    a.Branch = target.BranchName;
                foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == target.Id))
                    e.EntityBranch = target.BranchName;
            }
            affected = linkedDocs.Count;

            var entryDelegates = await _users.ListEntityManagersByEntryIdAsync(source.Id, token);
            foreach (var del in entryDelegates)
            {
                del.PortalGroupId = target.GroupId;
                del.PortalEntryId = target.Id;
            }

            var aliasesAdded = 0;
            AddFoldAliases(target, source.Group.CanonicalName, source, ref aliasesAdded);
            source.IsActive = false;
            await _uow.SaveChangesAsync(token);

            if (linkedDocs.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(linkedDocs, actor, token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Merge,
                groupId,
                sourceEntryId = source.Id,
                targetEntryId = target.Id,
                sourceBranchName = source.BranchName,
                targetBranchName = target.BranchName,
                governorate = target.Governorate,
                aliasesAdded,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = target.Id,
                GroupId = groupId,
                ActionKind = ActionKindCatalog.Merge,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);
            changeEventId = changeEvent.Id;

            await InsertBranchOccurrencesAsync(linkedDocs,
                $"تم دمج فرع «{source.Group.CanonicalName}» ({target.Governorate}/{source.BranchName}) في ({target.Governorate}/{target.BranchName})",
                actor, token);
            await InsertBranchChangeAlertAsync(target,
                $"تم دمج فرع جهة «{source.Group.CanonicalName}» ({target.Governorate}/{source.BranchName}) في ({target.Governorate}/{target.BranchName})",
                actor, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "merge_branches",
                details: $"دمج فرع: «{source.Group.CanonicalName}» ({target.Governorate}/{source.BranchName}) في ({target.Governorate}/{target.BranchName}) — {affected} ملفًا متأثرًا", ct: token);
        }, ct);

        return new MergeBranchesResponse(source.Id, target.Id, affected, changeEventId);
    }

    /// <inheritdoc/>
    public async Task<AbolishBranchResponse> AbolishBranchAsync(
        int groupId,
        int entryId,
        AbolishBranchRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        var entry = await GetActiveGroupEntryAsync(groupId, entryId, actor, ct);
        var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, ct);

        // «آخر نشط» ممنوع: لا يبقى للمجموعة فرع نشط بعد الإلغاء.
        var remainingActive = entry.Group.Entries.Any(e => e.Id != entry.Id && e.IsActive);
        if (!remainingActive)
            throw new ArgumentException("لا يمكن إلغاء آخر فرع نشط في المجموعة");

        if (linkedDocs.Count > 0 && !request.TargetEntryId.HasValue)
            throw new ArgumentException("الفرع مرتبط بملفات ولا يمكن إلغاؤه دون فرع هدف بديل (اختر قيدًا آخر للدمج)");

        // فرع هدف: دمج ضمني (سلوك دمج كامل — S4).
        PublicEntity? target = null;
        if (request.TargetEntryId.HasValue)
        {
            if (request.TargetEntryId.Value == entry.Id)
                throw new ArgumentException("لا يمكن دمج الفرع مع نفسه");
            target = await GetActiveGroupEntryAsync(groupId, request.TargetEntryId.Value, actor, ct);
            if (!string.Equals(entry.Governorate, target.Governorate, StringComparison.Ordinal))
                throw new ArgumentException("المحافظتان مختلفتان — الدمج يتطلب نفس المحافظة");
            if (entry.NeedsReview || target.NeedsReview)
                throw new ArgumentException("لا يمكن الإلغاء لوجود قيد بانتظار المراجعة");
        }

        int affected = linkedDocs.Count;
        int changeEventId = 0;
        await _tx.RunAsync(async token =>
        {
            if (target is not null)
            {
                // دمج ضمني (نفس دورة الدمج الكاملة).
                foreach (var doc in linkedDocs)
                {
                    RepointEntryLinks(doc, entry.Id, target.Id);
                    foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == target.Id))
                        a.Branch = target.BranchName;
                    foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == target.Id))
                        e.EntityBranch = target.BranchName;
                }

                var entryDelegates = await _users.ListEntityManagersByEntryIdAsync(entry.Id, token);
                foreach (var del in entryDelegates)
                {
                    del.PortalGroupId = target.GroupId;
                    del.PortalEntryId = target.Id;
                }

                var aliasesAdded = 0;
                AddFoldAliases(target, entry.Group.CanonicalName, entry, ref aliasesAdded);
                entry.IsActive = false;
                await _uow.SaveChangesAsync(token);

                if (linkedDocs.Count > 0)
                    await SyncAppealsAfterEntityChangeAsync(linkedDocs, actor, token);

                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    actionKind = ActionKindCatalog.Merge,
                    groupId,
                    sourceEntryId = entry.Id,
                    targetEntryId = target.Id,
                    sourceBranchName = entry.BranchName,
                    targetBranchName = target.BranchName,
                    governorate = target.Governorate,
                    aliasesAdded,
                });
                var changeEvent = new PublicEntityChangeEvent
                {
                    EntryId = target.Id,
                    GroupId = groupId,
                    ActionKind = ActionKindCatalog.Merge,
                    PayloadJson = payload,
                    ActorUserId = actor.UserId,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                await _changeEvents.AddAsync(changeEvent, token);
                await _uow.SaveChangesAsync(token);
                changeEventId = changeEvent.Id;

                await InsertBranchOccurrencesAsync(linkedDocs,
                    $"تم دمج فرع «{entry.Group.CanonicalName}» ({target.Governorate}/{entry.BranchName}) في ({target.Governorate}/{target.BranchName})",
                    actor, token);
                await InsertBranchChangeAlertAsync(target,
                    $"تم دمج فرع جهة «{entry.Group.CanonicalName}» ({target.Governorate}/{entry.BranchName}) في ({target.Governorate}/{target.BranchName})",
                    actor, token);
            }
            else
            {
                // تعطيل مباشر (صفر ملفات مرتبطة).
                var aliasesAdded = 0;
                AddEachExtraAlias(entry, FullEntryName(entry.Group.CanonicalName, entry.Governorate, entry.BranchName), ref aliasesAdded);
                entry.IsActive = false;
                await _uow.SaveChangesAsync(token);

                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    actionKind = ActionKindCatalog.Abolish,
                    groupId,
                    entryId,
                    branchName = entry.BranchName,
                    governorate = entry.Governorate,
                    aliasesAdded,
                });
                var changeEvent = new PublicEntityChangeEvent
                {
                    EntryId = entry.Id,
                    GroupId = groupId,
                    ActionKind = ActionKindCatalog.Abolish,
                    PayloadJson = payload,
                    ActorUserId = actor.UserId,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                await _changeEvents.AddAsync(changeEvent, token);
                await _uow.SaveChangesAsync(token);
                changeEventId = changeEvent.Id;

                await InsertBranchChangeAlertAsync(entry,
                    $"تم إلغاء فرع جهة «{entry.Group.CanonicalName}» ({entry.Governorate}/{entry.BranchName}) بلا ملفات مرتبطة",
                    actor, token);
            }

            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "abolish_branch",
                details: $"ألغى فرع: «{entry.Group.CanonicalName}» ({entry.Governorate}/{entry.BranchName})"
                    + (target is not null ? $" بدمج ضمني في «{target.BranchName}»" : " (تعطيل مباشر)") + $" — {affected} ملفًا متأثرًا", ct: token);
        }, ct);

        return new AbolishBranchResponse(entry.Id, target?.Id, affected, changeEventId);
    }

    /// <inheritdoc/>
    public async Task<UnifyBranchesResponse> UnifyBranchesAsync(
        int groupId,
        UnifyBranchesRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        var target = await GetActiveGroupEntryAsync(groupId, request.TargetEntryId, actor, ct);
        var absorbedIds = request.AbsorbedEntryIds?.Where(x => x != target.Id).Distinct().ToList()
            ?? new List<int>();
        if (absorbedIds.Count == 0)
            throw new ArgumentException("لا توجد فروع محددة للتوحيد (فرع واحد على الأقل غير الهدف)");

        var absorbed = new List<PublicEntity>();
        foreach (var absorbedId in absorbedIds)
        {
            var ae = await GetActiveGroupEntryAsync(groupId, absorbedId, actor, ct);
            if (!string.Equals(ae.Governorate, target.Governorate, StringComparison.Ordinal))
                throw new ArgumentException($"فرع «{ae.BranchName}» في محافظة مختلفة — التوحيد يتطلب نفس المحافظة");
            if (ae.NeedsReview || target.NeedsReview)
                throw new ArgumentException("لا يمكن التوحيد لوجود قيد بانتظار المراجعة");
            absorbed.Add(ae);
        }

        // تصحيح كتابة اسم الناجي (اختياري): يُغيّر فرع الهدف ويزامن لقطاته (S7).
        string? correctedName = null;
        if (!string.IsNullOrWhiteSpace(request.CorrectedName)
            && !string.Equals(request.CorrectedName.Trim(), target.BranchName, StringComparison.Ordinal))
        {
            correctedName = Required(request.CorrectedName, "اسم الفرع مطلوب", 200);
            await EnsureNoDuplicateEntryAsync(target.Id, target.Group.CanonicalName, target.Governorate, correctedName, ct);
        }

        int affected = 0;
        int changeEventId = 0;
        await _tx.RunAsync(async token =>
        {
            var affectedDocs = new Dictionary<int, Document>();
            var aliasesAdded = 0;

            foreach (var ae in absorbed)
            {
                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, token);
                foreach (var doc in linkedDocs)
                {
                    RepointEntryLinks(doc, ae.Id, target.Id);
                    foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == target.Id))
                        a.Branch = target.BranchName;
                    foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == target.Id))
                        e.EntityBranch = target.BranchName;
                    affectedDocs[doc.Id] = doc;
                }

                var entryDelegates = await _users.ListEntityManagersByEntryIdAsync(ae.Id, token);
                foreach (var del in entryDelegates)
                {
                    del.PortalGroupId = target.GroupId;
                    del.PortalEntryId = target.Id;
                }

                AddFoldAliases(target, ae.Group.CanonicalName, ae, ref aliasesAdded);
                ae.IsActive = false;
            }
            await _uow.SaveChangesAsync(token);

            if (correctedName is not null)
            {
                target.BranchName = correctedName;
                var labelDocs = await SyncBranchLabelsAsync(target, correctedName, token);
                foreach (var doc in labelDocs)
                    affectedDocs[doc.Id] = doc;
            }
            await _uow.SaveChangesAsync(token);

            var docsList = affectedDocs.Values.ToList();
            affected = docsList.Count;
            if (docsList.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(docsList, actor, token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Unify,
                groupId,
                targetEntryId = target.Id,
                targetBranchName = target.BranchName,
                absorbedEntryIds = absorbed.Select(a => a.Id).ToList(),
                absorbedBranchNames = absorbed.Select(a => a.BranchName).ToList(),
                correctedName,
                governorate = target.Governorate,
                aliasesAdded,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = target.Id,
                GroupId = groupId,
                ActionKind = ActionKindCatalog.Unify,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);
            changeEventId = changeEvent.Id;

            await InsertBranchOccurrencesAsync(docsList,
                $"تم توحيد تسميات فروع «{target.Group.CanonicalName}» ({target.Governorate}/{target.BranchName})", actor, token);
            await InsertBranchChangeAlertAsync(target,
                $"تم توحيد تسميات فروع جهة «{target.Group.CanonicalName}» ({target.Governorate}/{target.BranchName})", actor, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "unify_branches",
                details: $"وحّد {absorbed.Count} فروعًا في «{target.Group.CanonicalName}» ({target.Governorate}/{target.BranchName}) — {affected} ملفًا متأثرًا", ct: token);
        }, ct);

        return new UnifyBranchesResponse(target.Id, absorbed.Count, affected, changeEventId);
    }

    // ── اقتراح تعديل الجهة الأم (رئيس القسم → تبويب الإدارة) ──

    /// <inheritdoc/>
    public async Task<ParentEditSuggestionDto> SuggestParentEditAsync(
        int entryId,
        SuggestParentEditRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        if (actor.Role != UserRole.Head)
            throw new UnauthorizedAccessException("اقتراح تعديل الجهة الأم متاح لرئيس القسم فقط");
        if (!actor.BranchId.HasValue)
            throw new UnauthorizedAccessException("حسابك غير مرتبط بفرع لتقديم اقتراح");

        var entry = await _entities.GetEntryWithDetailsAsync(entryId, ct)
            ?? throw new ArgumentException("القيد غير موجود");
        if (!entry.IsParentEntity)
            throw new ArgumentException("الاقتراح يخص قيد «الجهة الأم» فقط");
        var group = entry.Group;
        if (!group.IsActive)
            throw new ArgumentException("المجموعة غير نشطة");

        // شرط النطاق: للمجموعة فرع نشط في محافظة فرع الرئيس.
        var headBranch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
        var headGov = NormalizeOptional(headBranch?.Governorate);
        if (headGov is null || !group.Entries.Any(e => e.IsActive && e.Governorate == headGov))
            throw new UnauthorizedAccessException("لا توجد فروع نشطة لهذه الجهة في محافظة فرعك");

        var proposedCanonical = NormalizeOptional(request.ProposedCanonicalName);
        if (proposedCanonical is not null)
        {
            if (ArabicNameNormalizer.Normalize(proposedCanonical) == ArabicNameNormalizer.Normalize(group.CanonicalName))
                throw new ArgumentException("الاسم المقترح مطابق للاسم الحالي");
            await EnsureCanonicalAvailableAsync(proposedCanonical, group.Id, ct);
        }
        var proposedType = NormalizeOptional(request.ProposedEntityType) is { } pt ? ValidEntityType(pt) : null;
        var proposedCitation = NormalizeOptional(request.ProposedCitationFormula) is { } pc ? ValidCitationFormula(pc, CitationFormulaCatalog.AddToJob) : null;
        var reason = Required(request.Reason, "سبب الاقتراح مطلوب", 500);

        // منع المكرر المعلّق (GroupId × فرع الرئيس) — الانعكاس خادمي وفهرس فريد جزئي يعزّزه.
        var all = await _suggestions.ListAsync(ct);
        if (all.Any(s => s.Status == ParentEditSuggestionStatusCatalog.Pending
            && s.GroupId == group.Id
            && s.CreatedBranchId == actor.BranchId.Value))
            throw new ArgumentException("يوجد اقتراح معلّق بالفعل لهذه الجهة من فرعك");

        var suggestion = new ParentEditSuggestion
        {
            GroupId = group.Id,
            EntryId = entry.Id,
            ProposedCanonicalName = proposedCanonical,
            ProposedEntityType = proposedType,
            ProposedCitationFormula = proposedCitation,
            Reason = reason,
            Status = ParentEditSuggestionStatusCatalog.Pending,
            CreatedById = actor.UserId,
            CreatedBranchId = actor.BranchId.Value,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _suggestions.AddAsync(suggestion, token);
            await _uow.SaveChangesAsync(token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Propose,
                groupId = group.Id,
                entryId = entry.Id,
                canonicalName = group.CanonicalName,
                proposedCanonicalName = proposedCanonical,
                proposedEntityType = proposedType,
                proposedCitationFormula = proposedCitation,
                reason,
                createdBranchId = actor.BranchId,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = entry.Id,
                GroupId = group.Id,
                ActionKind = ActionKindCatalog.Propose,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "suggest_parent_edit",
                details: $"اقترح تعديل الجهة الأم «{group.CanonicalName}»{($" — {proposedCanonical}")} {reason}", ct: token);
        }, ct);

        return ToParentEditSuggestionDto(suggestion);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ParentEditSuggestionDto>> ListParentEditSuggestionsAsync(
        ParentEditSuggestionListQuery query,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        var all = await _suggestions.ListAsync(ct);
        var status = NormalizeOptional(query.Status);
        IEnumerable<ParentEditSuggestion> filtered = all
            .Where(s => status is null || s.Status == status)
            .Where(s => query.GroupId is null || s.GroupId == query.GroupId);

        // نطاق رئيس القسم: اقتراحاته هو فقط (لحالة المعلّق في نافذة فروع جهة محافظته).
        if (actor.Role == UserRole.Head)
            filtered = filtered.Where(s => s.CreatedById == actor.UserId);

        var ordered = filtered.OrderByDescending(s => s.CreatedAtUtc).ToList();
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * perPage).Take(perPage).Select(ToParentEditSuggestionDto).ToList();
        return new PagedResult<ParentEditSuggestionDto> { Items = items, Page = page, PerPage = perPage, TotalCount = total };
    }

    /// <inheritdoc/>
    public async Task<ParentEditSuggestionDto?> ReviewParentEditSuggestionAsync(
        int suggestionId,
        ReviewParentEditSuggestionRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        if (actor.Role is not (UserRole.Manager or UserRole.Admin))
            throw new UnauthorizedAccessException("القرار على اقتراحات الجهة الأم متاح للمدير/المشرف فقط");

        var suggestion = await _suggestions.GetByIdAsync(suggestionId, ct);
        if (suggestion is null)
            return null;
        if (suggestion.Status != ParentEditSuggestionStatusCatalog.Pending)
            throw new ArgumentException("الاقتراح لم يعد معلّقًا");

        var status = Required(request.Status, "الحالة مطلوبة", 20);
        if (status is not (ParentEditSuggestionStatusCatalog.Approved or ParentEditSuggestionStatusCatalog.Rejected))
            throw new ArgumentException("الحالة يجب أن تكون approved أو rejected");
        var reviewReason = NormalizeOptional(request.ReviewReason);
        if (status == ParentEditSuggestionStatusCatalog.Rejected)
            reviewReason = Required(reviewReason, "سبب الرفض مطلوب", 500);

        // حوكمة S1: لا يُعتمد اسم مقترح لم يُطبَّق بعد — مدراء/مشرفون يجيزون التغيير المنجز
        // فعليًا على الأرض، والواجهة تطبّقه قبل المراجعة (needsRename)؛ المسار المباشر يُجبَر هنا.
        var proposedName = NormalizeOptional(suggestion.ProposedCanonicalName);
        if (status == ParentEditSuggestionStatusCatalog.Approved && proposedName is not null)
        {
            var liveGroup = await _entities.GetGroupAsync(suggestion.GroupId, ct)
                ?? throw new ArgumentException("المجموعة غير موجودة");
            if (ArabicNameNormalizer.Normalize(proposedName) != ArabicNameNormalizer.Normalize(liveGroup.CanonicalName))
                throw new ArgumentException("اعتماد اقتراح باسم غير مُطبَّق مرفوض — طبّق إعادة التسمية أولًا ثم أعد المراجعة");
        }

        suggestion.Status = status;
        suggestion.ReviewedById = actor.UserId;
        suggestion.ReviewReason = reviewReason;
        suggestion.ReviewedAtUtc = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "review_parent_edit_suggestion",
                details: $"{(status == ParentEditSuggestionStatusCatalog.Approved ? "قبل" : "رفض")} اقتراح تعديل الجهة الأم «{suggestion.Group?.CanonicalName ?? suggestion.Entry?.Group?.CanonicalName}»{($" — {reviewReason}")}",
                ct: token);
        }, ct);

        return ToParentEditSuggestionDto(suggestion);
    }

    /// <inheritdoc/>
    public async Task<ParentEditSuggestionDto?> WithdrawParentEditSuggestionAsync(
        int suggestionId,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        if (actor.Role != UserRole.Head)
            throw new UnauthorizedAccessException("سحب اقتراح الجهة الأم متاح لمنشئه رئيس القسم فقط");

        var suggestion = await _suggestions.GetByIdAsync(suggestionId, ct);
        if (suggestion is null)
            return null;
        if (suggestion.CreatedById != actor.UserId)
            throw new UnauthorizedAccessException("لا يمكنك سحب اقتراح منشأ من رئيس قسم آخر");
        if (suggestion.Status != ParentEditSuggestionStatusCatalog.Pending)
            throw new ArgumentException("الاقتراح لم يعد معلّقًا فلا يُسحب");

        suggestion.Status = ParentEditSuggestionStatusCatalog.Withdrawn;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "withdraw_parent_edit_suggestion",
                details: $"سحب اقتراحه لتعديل الجهة الأم «{suggestion.Group?.CanonicalName ?? suggestion.Entry?.Group?.CanonicalName}»",
                ct: token);
        }, ct);

        return ToParentEditSuggestionDto(suggestion);
    }

    private static ParentEditSuggestionDto ToParentEditSuggestionDto(ParentEditSuggestion s)
    {
        var canonical = s.Entry?.Group?.CanonicalName ?? s.Group?.CanonicalName ?? string.Empty;
        return new ParentEditSuggestionDto(
            s.Id,
            s.GroupId,
            s.EntryId,
            canonical,
            s.Entry?.Group?.EntityType ?? s.Group?.EntityType ?? string.Empty,
            s.ProposedCanonicalName,
            s.ProposedEntityType,
            s.ProposedCitationFormula,
            s.Reason,
            s.Status,
            s.CreatedById,
            s.CreatedBy?.FullName ?? s.CreatedBy?.Username ?? string.Empty,
            s.CreatedBranchId,
            s.ReviewedById,
            s.ReviewReason,
            s.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            s.ReviewedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture));
    }

    // ── مساعدات خاصة ──

    private async Task EnsureHeadScopeAsync(EntityRegistryActor actor, PublicEntity? entry, string? fallbackGovernorate, CancellationToken ct)
    {
        if (actor.Role != UserRole.Head)
            return;
        var branch = actor.BranchId is null ? null : await _branches.GetByIdAsync(actor.BranchId.Value, ct);
        var branchGov = NormalizeOptional(branch?.Governorate);

        // نطاق رئيس القسم (قرار مالك المشروع): يدير ويراجع ما أدخله محامو فرعه،
        // بغض النظر عن المحافظة التي تتبع لها الجهة نفسها — فقد يُقيم محامٍ ملفًا
        // تنفيذيًا على جهة عامة تتبع محافظة أخرى. إضافةً إلى قيود محافظة فرعه
        // التي أدخلتها الإدارة (بلا محامٍ مُدخِل).
        if (entry?.CreatedBy is { BranchId: not null } creator)
        {
            var inCreatorBranch = creator.BranchId.Value == actor.BranchId;
            var inGovernorate = branchGov is not null
                && string.Equals(branchGov, entry.Governorate.Trim(), StringComparison.Ordinal);
            if (inCreatorBranch || inGovernorate)
                return;
            throw new UnauthorizedAccessException(
                "رئيس القسم مقصور على ما أدخله محامو فرعه أو قيود محافظة فرعه؛ اطلب من الإدارة ضبط محافظة الفرع أولًا");
        }

        var scopeGov = fallbackGovernorate ?? entry?.Governorate;
        if (branchGov is null || scopeGov is null || !string.Equals(branchGov, scopeGov.Trim(), StringComparison.Ordinal))
            throw new UnauthorizedAccessException(
                "رئيس القسم مقصور على ما أدخله محامو فرعه أو قيود محافظة فرعه؛ اطلب من الإدارة ضبط محافظة الفرع أولًا");
    }

    /// <summary>
    /// حارس الجهة الأم (C3/F3): رئيس القسم لا يحرّر قيد «الجهة الأم» إطلاقًا في أي مسار
    /// كتابة — يقتصر على إرسال اقتراح تعديل للإدارة.
    /// </summary>
    private static void GuardHeadCannotEditParent(EntityRegistryActor actor, PublicEntity entry)
    {
        if (actor.Role == UserRole.Head && entry.IsParentEntity)
            throw new UnauthorizedAccessException(
                "الجهة الأم تُدار عبر الاقتراح فقط — أرسل اقتراح تعديل للإدارة");
    }

    /// <summary>
    /// حارس الأم البنيوي (S1 — منع صريح للجميع): عمليات الفروع الأربع لا تستهدف قيد «الجهة الأم»
    /// ممن كان؛ طريقها الوحيد المسارات المركزية (Update / rename / AbolishAndReplace المجموعي).
    /// </summary>
    private static void GuardNotParentEntry(PublicEntity entry, string? message = null)
    {
        if (entry.IsParentEntity)
            throw new ArgumentException(
                message ?? "عمليات الفروع للفروع فقط — الأم تُدار عبر التعديل أو إعادة التسمية المركزية");
    }

    // ── مساعدات عمليات الفروع المشتركة (رئيس القسم — ضمن محافظته) ──

    /// <summary>
    /// يجلب مجموعة نشطة وقيدًا نشطًا تابعًا لها (بها قابلية الحارس الأم والنطاق).
    /// يُستخدم لكل عمليات الفروع الأربع.
    /// </summary>
    private async Task<PublicEntity> GetActiveGroupEntryAsync(
        int groupId,
        int entryId,
        EntityRegistryActor actor,
        CancellationToken ct)
    {
        var group = await _entities.GetGroupAsync(groupId, ct)
            ?? throw new ArgumentException("المجموعة غير موجودة");
        if (!group.IsActive)
            throw new ArgumentException("المجموعة غير نشطة");
        var entry = await _entities.GetEntryWithDetailsAsync(entryId, ct)
            ?? throw new ArgumentException("القيد غير موجود");
        if (entry.GroupId != groupId)
            throw new ArgumentException("القيد لا ينتمي إلى المجموعة المحددة");
        if (!entry.IsActive)
            throw new ArgumentException("القيد غير نشط");
        GuardNotParentEntry(entry);
        GuardHeadCannotEditParent(actor, entry);
        await EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
        return entry;
    }

    /// <summary>
    /// مزامنة لقطات فروع الجهة في كل الملفات المرتبطة بالقيد (نشطة/مشطوبة/تريث) — S7:
    /// تحديث عمودي Branch/EntityBranch للصفوف ذات RegistryId==entryId بلا إعادة بناء
    /// SearchText (الفرع ليس جزءًا من نص البحث — F1). تُرجع الملفات المتأثرة لكتابة الوقوعات.
    /// </summary>
    private async Task<List<Document>> SyncBranchLabelsAsync(
        PublicEntity entry,
        string newBranch,
        CancellationToken token)
    {
        var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, token);
        if (linkedDocs.Count == 0)
            return linkedDocs;
        foreach (var doc in linkedDocs)
        {
            foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == entry.Id))
                a.Branch = newBranch;
            foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == entry.Id))
                e.EntityBranch = newBranch;
        }
        await _uow.SaveChangesAsync(token);
        return linkedDocs;
    }

    /// <summary>وقفعة «تغيير جهة» آلية لكل ملف متأثر بعملية فرع (نفس نمط النقل/الدمج).</summary>
    private async Task InsertBranchOccurrencesAsync(
        IReadOnlyCollection<Document> docs,
        string details,
        EntityRegistryActor actor,
        CancellationToken token)
    {
        if (docs.Count == 0)
            return;
        foreach (var doc in docs)
        {
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                EventDate = DateTime.UtcNow,
                CreatedById = actor.UserId,
                Details = details,
            }, token);
        }
    }

    /// <summary>تنبيه فرعي لرؤساء محافظة القيد بتغيير فرع جهة عامة ضمن محافظتهم.</summary>
    private async Task InsertBranchChangeAlertAsync(
        PublicEntity entry,
        string message,
        EntityRegistryActor actor,
        CancellationToken token)
    {
        var heads = await _entities.ListActiveHeadsByGovernorateAsync(entry.Governorate, token);
        foreach (var head in heads.Where(h => h.BranchId.HasValue))
        {
            var alert = new HeadAlert
            {
                BranchId = head.BranchId!.Value,
                CreatedById = actor.UserId,
                PublicEntityId = entry.Id,
                TargetType = HeadAlertTargetType.Branch,
                Message = message.Length > 2000 ? message[..2000] : message,
                CreatedAt = DateTime.UtcNow,
                Recipients = { new HeadAlertRecipient { UserId = head.Id } },
            };
            await _headAlerts.AddAsync(alert, token);
        }
    }

    /// <summary>اسم بديل «للبحث فقط» على قيد: الاسم الكامل القديم (المعتمد — المحافظة / الفرع).</summary>
    private static void AddEachExtraAlias(PublicEntity entry, string text, ref int aliasesAdded)
    {
        var norm = ArabicNameNormalizer.Normalize(text);
        if (norm.Length == 0 || entry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == norm))
            return;
        entry.Aliases.Add(new PublicEntityAlias { PublicEntityId = entry.Id, AliasText = text });
        aliasesAdded++;
    }

    private static string FullEntryName(string canonical, string governorate, string branchName)
        => $"{canonical} — {governorate} / {branchName}";

    private async Task<PublicEntityGroup> FindOrCreateGroupAsync(string canonical, string entityType, int actorUserId, CancellationToken token)
    {
        var norm = ArabicNameNormalizer.Normalize(canonical);
        var existing = await FindGroupByNormAsync(norm, token);
        if (existing is not null)
            return existing;
        return new PublicEntityGroup
        {
            CanonicalName = canonical,
            EntityType = entityType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    private async Task<PublicEntityGroup?> FindGroupByNormAsync(string norm, CancellationToken token)
    {
        if (norm.Length == 0)
            return null;
        // متتبَّعة عمدًا: القيد الجديد يشير إليها دون إعادة إدراجها (تعارض تتبع).
        var groups = await _entities.ListGroupsTrackedAsync(token);
        return groups.FirstOrDefault(g => ArabicNameNormalizer.Normalize(g.CanonicalName) == norm);
    }

    private async Task EnsureCanonicalAvailableAsync(string canonical, int excludeGroupId, CancellationToken ct)
    {
        var norm = ArabicNameNormalizer.Normalize(canonical);
        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        if (groups.Any(g => g.Id != excludeGroupId && ArabicNameNormalizer.Normalize(g.CanonicalName) == norm))
            throw new ArgumentException("اسم الجهة مستخدم مسبقًا لهوية أخرى");
    }

    private async Task EnsureNoDuplicateEntryAsync(int? excludeEntryId, string canonical, string governorate, string branchName, CancellationToken ct)
    {
        var norm = ArabicNameNormalizer.Normalize(canonical);
        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        var duplicated = groups
            .Where(g => ArabicNameNormalizer.Normalize(g.CanonicalName) == norm)
            .SelectMany(g => g.Entries)
            .Any(e => (excludeEntryId is null || e.Id != excludeEntryId)
                && e.Governorate == governorate && e.BranchName == branchName);
        if (duplicated)
            throw new ArgumentException("يوجد قيد لنفس الجهة بنفس المحافظة والفرع");
    }

    private static List<string> CleanAliases(IEnumerable<string>? aliases, string canonicalNorm)
    {
        var result = new List<string>();
        if (aliases is null)
            return result;
        var seen = new HashSet<string>(StringComparer.Ordinal) { canonicalNorm };
        foreach (var raw in aliases)
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.Length == 0)
                continue;
            if (text.Length > 500)
                throw new ArgumentException("الاسم البديل أطول من 500 حرف");
            if (!seen.Add(ArabicNameNormalizer.Normalize(text)))
                continue;
            result.Add(text);
        }
        return result;
    }

    /// <summary>قيمة إلزامية بعد القصّ والتنظيف؛ الفارغ يُرفض برسالة.</summary>
    private static string Required(string? value, string emptyMessage, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException(emptyMessage);
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{emptyMessage} — أقصى طول {maxLength}");
        return normalized;
    }

    /// <summary>قيمة اختيارية ببديل افتراضي معتمد («الجهة الأم») مع سقف الطول.</summary>
    private static string RequiredWithFallback(string? value, string fallback, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = fallback;
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{fallback} — أقصى طول {maxLength}");
        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string ValidEntityType(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (!PublicEntityTypeCatalog.IsValid(trimmed))
            throw new ArgumentException($"نوع الجهة غير صالح ({string.Join("/", PublicEntityTypeCatalog.All)})");
        return trimmed;
    }

    private static string ValidCitationFormula(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var trimmed = value.Trim().ToLowerInvariant();
        if (!CitationFormulaCatalog.IsValid(trimmed))
            throw new ArgumentException("صيغة المناداة غير صالحة (add-to-job/add-to-position)");
        return trimmed;
    }

    /// <summary>تحقق تسمية التغطية: فارغ → null؛ أطول من 150 → خطأ؛ مطابقة لمحافظة → خطأ.</summary>
    private static string? ValidateCoverageLabel(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;
        if (trimmed.Length > 150)
            throw new ArgumentException("تسمية التغطية أطول من 150 حرفًا");
        if (GovernorateCatalog.IsGovernorate(trimmed))
            throw new ArgumentException("تسمية التغطية لا يمكن أن تطابق اسم محافظة واحدة");
        if (trimmed.Any(c => c >= '\u0660' && c <= '\u0669'))
            throw new ArgumentException("تسمية التغطية لا تقبل أرقامًا عربية-هندية");
        return trimmed;
    }

    private static string? Clamp(string? value)
        => value is null ? null : DocumentSearchTextBuilder.Truncate(value);

    // ── مساعدا الطيّ المشتركان (الدمج/النقل-طي/التوحيد) ──
    // دلالة حرفية لمنطق الدمج (المرجع الأكمل): إعادة توجيه روابط القيد الثلاث وإعادة اشتقاق
    // المركّب، ثم الأسماء البديلة (المعياري + الكامل + سوابق القيد الممتصّ) بشروط الاستثناء نفسها.

    /// <summary>يعيد توجيه روابط قيد في مستند إلى قيد آخر ويعيد اشتقاق المركّب.</summary>
    private static void RepointEntryLinks(Document doc, int fromEntryId, int toEntryId)
    {
        foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == fromEntryId))
            a.RegistryId = toEntryId;
        foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == fromEntryId))
            e.RegistryId = toEntryId;
        foreach (var ea in doc.ExecutionApplicants.Where(ea => ea.RegistryId == fromEntryId))
            ea.RegistryId = toEntryId;
        doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);
    }

    /// <summary>أسماء بديلة «للبحث فقط» على قيد الناجي: المعياري + الكامل + سوابق القيد الممتصّ.</summary>
    private static void AddFoldAliases(PublicEntity targetEntry, string absorbedGroupName, PublicEntity absorbedEntry, ref int aliasesAdded)
    {
        var fullName = $"{absorbedGroupName} — {absorbedEntry.Governorate} / {absorbedEntry.BranchName}";
        var normalizedEntry = ArabicNameNormalizer.Normalize(absorbedGroupName);
        if (!targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normalizedEntry))
        {
            targetEntry.Aliases.Add(new PublicEntityAlias
            {
                PublicEntityId = targetEntry.Id,
                AliasText = absorbedGroupName,
            });
            aliasesAdded++;
        }
        var normalizedFull = ArabicNameNormalizer.Normalize(fullName);
        if (!targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normalizedFull))
        {
            targetEntry.Aliases.Add(new PublicEntityAlias
            {
                PublicEntityId = targetEntry.Id,
                AliasText = fullName,
            });
            aliasesAdded++;
        }
        foreach (var priorAlias in absorbedEntry.Aliases)
        {
            var priorNorm = ArabicNameNormalizer.Normalize(priorAlias.AliasText);
            if (priorNorm.Length == 0
                || priorNorm == normalizedEntry
                || priorNorm == normalizedFull
                || targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == priorNorm))
                continue;
            targetEntry.Aliases.Add(new PublicEntityAlias
            {
                PublicEntityId = targetEntry.Id,
                AliasText = priorAlias.AliasText,
            });
            aliasesAdded++;
        }
    }

    // ── نقل القيد (د3) ──

    /// <inheritdoc/>
    public async Task<MoveEntryResponse> MoveEntryAsync(int entryId, MoveEntryRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        if (request.TargetGroupId is null && request.TargetEntryId is null)
            throw new ArgumentException("حدّد الهوية الأم الهدف (TargetGroupId) أو القيد الهدف (TargetEntryId)");

        if (request.TargetEntryId.HasValue && request.TargetEntryId.Value == entryId)
            throw new ArgumentException("لا يمكن طيّ قيد على نفسه");

        return await _tx.RunAsync(async token =>
        {
            var entry = await _entities.GetEntryWithDetailsAsync(entryId, token)
                ?? throw new ArgumentException("القيد غير موجود");

            if (entry.NeedsReview)
                throw new ArgumentException("لا يمكن نقل قيد بانتظار المراجعة؛ اعتمده أولًا");

            // حوكمة S1 — منع صريح للجميع: قيد «الجهة الأم» لا يُنقَل ولا يُطوى أصلًا؛
            // الحارس على المصدر فقط، فالطيُّ باتجاه أمّ هدفٍ يبقى مباحًا في وضع (ب).
            GuardNotParentEntry(entry, "القيد الأم لا يُنقَل ولا يُطوى — إعادة الهيكلة عبر الإلغاء والاستبدال المركزي");

            var fromGroupId = entry.GroupId;
            var fromGroupName = entry.Group.CanonicalName;
            int toGroupId;
            int affectedDocs = 0;
            int targetEntryId;
            var affectedDocIds = new List<int>();

            if (request.TargetEntryId.HasValue)
            {
                // وضع ب: الطيّ في قيد مطابق
                var targetEntry = await _entities.GetEntryAsync(request.TargetEntryId.Value, token)
                    ?? throw new ArgumentException("القيد الهدف غير موجود");
                if (!targetEntry.IsActive)
                    throw new ArgumentException("القيد الهدف غير نشط");
                if (targetEntry.Governorate != entry.Governorate || targetEntry.BranchName != entry.BranchName)
                    throw new ArgumentException("الطيّ يتطلب مطابقة المحافظة والفرع");

                toGroupId = targetEntry.GroupId;
                // نطاق رئيس القسم: القيد المنقول نفسه يجب أن يكون ضمن نطاقه (لمحامٍ من فرعه).
                await EnsureHeadScopeAsync(actor, entry, entry.Governorate, token);
                targetEntryId = targetEntry.Id;

                // ترحيل روابط RegistryId
                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entryId, token);
                affectedDocIds.AddRange(linkedDocs.Select(d => d.Id));
                foreach (var doc in linkedDocs)
                    RepointEntryLinks(doc, entryId, targetEntryId);
                affectedDocs = linkedDocs.Count;

                // ترحيل مندوبي مستوى القيد إلى القيد الناجي (نطاق بوابة المحاماة يتبدل مع القيد المطوي)
                var entryDelegates = await _users.ListEntityManagersByEntryIdAsync(entryId, token);
                foreach (var del in entryDelegates)
                {
                    del.PortalGroupId = targetEntry.GroupId;
                    del.PortalEntryId = targetEntryId;
                }

                // إيقاف القيد المنقول
                entry.IsActive = false;

                // إضافة الاسم الكامل كاسم بديل للهدف
                var fullName = $"{entry.Group.CanonicalName} — {entry.Governorate} / {entry.BranchName}";
                var normalizedEntry = ArabicNameNormalizer.Normalize(entry.Group.CanonicalName);
                if (!targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normalizedEntry))
                {
                    targetEntry.Aliases.Add(new PublicEntityAlias
                    {
                        PublicEntityId = targetEntry.Id,
                        AliasText = entry.Group.CanonicalName,
                    });
                }
                // إضافة النص الكامل أيضًا
                var normalizedFull = ArabicNameNormalizer.Normalize(fullName);
                if (!targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normalizedFull))
                {
                    targetEntry.Aliases.Add(new PublicEntityAlias
                    {
                        PublicEntityId = targetEntry.Id,
                        AliasText = fullName,
                    });
                }

                // مزامنة النصوص
                await SyncTextsAfterFoldAsync(linkedDocs, actor.Name, token);
            }
            else
            {
                // وضع أ: تغيير الهوية الأم
                var targetGroup = await _entities.GetGroupAsync(request.TargetGroupId!.Value, token)
                    ?? throw new ArgumentException("الهوية الأم الهدف غير موجودة");
                if (!targetGroup.IsActive)
                    throw new ArgumentException("الهوية الأم الهدف غير نشطة");
                if (targetGroup.Id == entry.GroupId)
                    throw new ArgumentException("القيد موجود مسبقًا في الهوية الأم الهدف");
                toGroupId = targetGroup.Id;
                targetEntryId = entryId;

                await EnsureHeadScopeAsync(actor, entry, entry.Governorate, token);

                // فحص تعارض المحافظة والفرع
                var conflict = await _entities.FindEntryInGroupAsync(toGroupId, entry.Governorate, entry.BranchName, token);
                if (conflict is not null)
                    throw new ArgumentException(
                        $"يوجد قيد مطابق ({conflict.BranchName}) في الهوية الهدف؛ استخدم وضع الطيّ (TargetEntryId={conflict.Id}) بدلاً من ذلك");

                entry.GroupId = toGroupId;
                entry.Group = targetGroup;

                // مزامنة النصوص
                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entryId, token);
                affectedDocIds.AddRange(linkedDocs.Select(d => d.Id));
                affectedDocs = linkedDocs.Count;
                foreach (var doc in linkedDocs)
                    doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);
                await SyncTextsAfterFoldAsync(linkedDocs, actor.Name, token);
            }

            // كتابة ChangeEvent
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                fromGroup = fromGroupName,
                toGroup = (await _entities.GetGroupAsync(toGroupId, token))?.CanonicalName ?? "",
                fromGroupId,
                toGroupId,
                entryName = entry.Group.CanonicalName,
                governorate = entry.Governorate,
                branchName = entry.BranchName,
                mode = request.TargetEntryId.HasValue ? "fold" : "reassign",
                affectedDocuments = affectedDocs,
                decreeKind = request.DecreeKind,
                decreeNumber = request.DecreeNumber,
                decreeDate = request.DecreeDate,
                note = request.Note,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = entryId,
                GroupId = fromGroupId,
                ActionKind = ActionKindCatalog.Move,
                DecreeKind = request.DecreeKind,
                DecreeNumber = request.DecreeNumber,
                DecreeDate = !string.IsNullOrEmpty(request.DecreeDate)
                    && DateTime.TryParse(request.DecreeDate, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var dd) ? dd : (DateTime?)null,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);

            await _uow.SaveChangesAsync(token);

            // وقوعات آلية لكل ملف متأثر
            if (affectedDocIds.Count > 0)
            {
                foreach (var docId in affectedDocIds.Distinct())
                {
                    var occurrence = new DocumentOccurrence
                    {
                        DocumentId = docId,
                        OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                        EventDate = DateTime.UtcNow,
                        CreatedById = actor.UserId,
                        Details = $"تم نقل قيد «{entry.Group.CanonicalName}» ({entry.Governorate}/{entry.BranchName})",
                    };
                    await _occurrences.AddAsync(occurrence, token);
                }
            }

            // تنبيه رئيس الهوية الجديدة
            var heads = await _entities.ListActiveHeadsByGovernorateAsync(entry.Governorate, token);
            var targetHeads = heads.Where(h => h.BranchId.HasValue);
            foreach (var head in targetHeads)
            {
                var msg = $"أُلحق بقيدكم فرع من هيئة أخرى: «{entry.Group.CanonicalName}» — {entry.Governorate}/{entry.BranchName}";
                var alert = new HeadAlert
                {
                    BranchId = head.BranchId!.Value,
                    CreatedById = actor.UserId,
                    TargetType = HeadAlertTargetType.Branch,
                    Message = msg.Length > 2000 ? msg[..2000] : msg,
                    CreatedAt = DateTime.UtcNow,
                    Recipients = { new HeadAlertRecipient { UserId = head.Id } },
                };
                await _headAlerts.AddAsync(alert, token);
            }

            await _uow.SaveChangesAsync(token);

            // تدقيق
            await _audit.LogAsync(actor.Name, "move_entity_registry",
                documentId: null, documentType: null,
                details: $"نقل قيد «{entry.Group.CanonicalName}» ({entry.Governorate}/{entry.BranchName}) من «{fromGroupName}» ← هوية #{toGroupId} — {affectedDocs} ملفًا متأثرًا",
                ct: token);

            return new MoveEntryResponse(entryId, fromGroupId, toGroupId, affectedDocs, changeEvent.Id);
        }, ct);
    }

    /// <inheritdoc/>
    public async Task<MoveAllEntriesResponse> MoveAllEntriesAsync(MoveAllEntriesRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        return await _tx.RunAsync(async token =>
        {
            var sourceGroup = await _entities.GetGroupAsync(request.SourceGroupId, token)
                ?? throw new ArgumentException("الهوية الأم المصدر غير موجودة");
            var targetGroup = await _entities.GetGroupAsync(request.TargetGroupId, token)
                ?? throw new ArgumentException("الهوية الأم الهدف غير موجودة");
            if (!targetGroup.IsActive)
                throw new ArgumentException("الهوية الأم الهدف غير نشطة");
            if (request.SourceGroupId == request.TargetGroupId)
                throw new ArgumentException("الهوية الأم المصدر والهدف متطابقتان");

            var sourceEntries = sourceGroup.Entries.Where(e => e.IsActive).ToList();
            if (sourceEntries.Count == 0)
                throw new ArgumentException("لا يوجد قيود نشطة في الهوية الأم المصدر");

            int totalAffectedDocs = 0;
            int entriesMoved = 0;
            var affectedDocIds = new List<int>();

            foreach (var entry in sourceEntries)
            {
                await EnsureHeadScopeAsync(actor, entry, entry.Governorate, token);

                // فحص تعارض
                var conflict = await _entities.FindEntryInGroupAsync(request.TargetGroupId, entry.Governorate, entry.BranchName, token);
                if (conflict is not null)
                    throw new ArgumentException(
                        $"تعارض: القيد «{entry.Governorate}/{entry.BranchName}» موجود مسبقًا في الهوية الهدف (قيد #{conflict.Id})");

                entry.GroupId = request.TargetGroupId;
                entry.Group = targetGroup;

                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, token);
                totalAffectedDocs += linkedDocs.Count;
                affectedDocIds.AddRange(linkedDocs.Select(d => d.Id));
                foreach (var doc in linkedDocs)
                    doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);

                entriesMoved++;
            }

            // ChangeEvent واحد لكل عملية نقل جماعي
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                fromGroup = sourceGroup.CanonicalName,
                toGroup = targetGroup.CanonicalName,
                fromGroupId = sourceGroup.Id,
                toGroupId = targetGroup.Id,
                entriesMoved,
                affectedDocuments = totalAffectedDocs,
                decreeKind = request.DecreeKind,
                decreeNumber = request.DecreeNumber,
                decreeDate = request.DecreeDate,
                note = request.Note,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = sourceGroup.Id,
                ActionKind = ActionKindCatalog.Move,
                DecreeKind = request.DecreeKind,
                DecreeNumber = request.DecreeNumber,
                DecreeDate = !string.IsNullOrEmpty(request.DecreeDate)
                    && DateTime.TryParse(request.DecreeDate, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var dd) ? dd : (DateTime?)null,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);

            await _uow.SaveChangesAsync(token);

            // وقوعات آلية لكل ملف متأثر
            if (affectedDocIds.Count > 0)
            {
                foreach (var docId in affectedDocIds.Distinct())
                {
                    var occurrence = new DocumentOccurrence
                    {
                        DocumentId = docId,
                        OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                        EventDate = DateTime.UtcNow,
                        CreatedById = actor.UserId,
                        Details = $"تم نقل قيد من «{sourceGroup.CanonicalName}» إلى «{targetGroup.CanonicalName}»",
                    };
                    await _occurrences.AddAsync(occurrence, token);
                }
            }

            await _uow.SaveChangesAsync(token);
            var affectedGovernorates = sourceEntries.Select(e => e.Governorate).Distinct().ToList();
            foreach (var gov in affectedGovernorates)
            {
                var heads = await _entities.ListActiveHeadsByGovernorateAsync(gov, token);
                foreach (var head in heads.Where(h => h.BranchId.HasValue))
                {
                    var msg = $"تم نقل جميع قيود «{sourceGroup.CanonicalName}» ({gov}) إلى «{targetGroup.CanonicalName}»";
                    var alert = new HeadAlert
                    {
                        BranchId = head.BranchId!.Value,
                        CreatedById = actor.UserId,
                        TargetType = HeadAlertTargetType.Branch,
                        Message = msg.Length > 2000 ? msg[..2000] : msg,
                        CreatedAt = DateTime.UtcNow,
                        Recipients = { new HeadAlertRecipient { UserId = head.Id } },
                    };
                    await _headAlerts.AddAsync(alert, token);
                }
            }

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "move_all_entity_registry",
                documentId: null, documentType: null,
                details: $"نقل جميع القيود ({entriesMoved}) من «{sourceGroup.CanonicalName}» ← «{targetGroup.CanonicalName}» — {totalAffectedDocs} ملفًا متأثرًا",
                ct: token);

            return new MoveAllEntriesResponse(sourceGroup.Id, targetGroup.Id, entriesMoved, totalAffectedDocs, changeEvent.Id);
        }, ct);
    }

    // ── الدمج N←1 (د5 §4) ──

    /// <inheritdoc/>
    public async Task<MergePreviewResponse> PreviewMergeAsync(MergePreviewRequest request, CancellationToken ct = default)
    {
        var survivorGroup = await _entities.GetGroupAsync(request.SurvivorGroupId, ct)
            ?? throw new ArgumentException("الهوية الأم الناجية غير موجودة");
        if (!survivorGroup.IsActive)
            throw new ArgumentException("الهوية الأم الناجية غير نشطة");

        if (request.AbsorbedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للدمج");

        if (request.AbsorbedGroupIds.Contains(request.SurvivorGroupId))
            throw new ArgumentException("لا يمكن دمج هوية في نفسها");

        var survivorEntryEntities = await _entities.ListEntriesByGroupAsync(survivorGroup.Id, ct);
        var activeSurvivorEntries = survivorEntryEntities.Where(e => e.IsActive).ToList();
        if (activeSurvivorEntries.Count == 0)
            throw new ArgumentException("الهوية الأم الناجية بلا قيود نشطة");

        var warnings = new List<string>();
        var absorbedDtos = new List<AbsorbedGroupPreviewDto>();
        int totalAffected = 0;

        foreach (var ae in survivorEntryEntities.Where(e => e.NeedsReview))
            warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{survivorGroup.CanonicalName}» (الناجي) بانتظار المراجعة");

        foreach (var absorbedId in request.AbsorbedGroupIds.Distinct())
        {
            var absorbedGroup = await _entities.GetGroupAsync(absorbedId, ct)
                ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
            if (!absorbedGroup.IsActive)
                throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");

            var absorbedEntryEntities = await _entities.ListEntriesByGroupAsync(absorbedId, ct);

            foreach (var ae in absorbedEntryEntities.Where(e => e.NeedsReview))
                warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{absorbedGroup.CanonicalName}» بانتظار المراجعة");

            var entryDtos = new List<AbsorbedEntryPreviewDto>();
            int groupDocCount = 0;

            foreach (var ae in absorbedEntryEntities)
            {
                var matchedSurvivor = activeSurvivorEntries
                    .FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);

                var defaultEntry = activeSurvivorEntries.First();
                int mappedToId = matchedSurvivor?.Id ?? defaultEntry.Id;
                bool conflictsWithSurvivor = matchedSurvivor is null;

                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, ct);
                int docCount = linkedDocs.Count;
                groupDocCount += docCount;

                entryDtos.Add(new AbsorbedEntryPreviewDto(
                    ae.Id, ae.Governorate, ae.BranchName,
                    docCount,
                    mappedToId, conflictsWithSurvivor));
            }

            totalAffected += groupDocCount;

            var aliases = absorbedEntryEntities
                .SelectMany(e => e.Aliases)
                .Select(a => a.AliasText)
                .Distinct()
                .ToList();

            absorbedDtos.Add(new AbsorbedGroupPreviewDto(
                absorbedGroup.Id, absorbedGroup.CanonicalName,
                entryDtos, groupDocCount, aliases));
        }

        return new MergePreviewResponse(survivorGroup.CanonicalName, absorbedDtos, totalAffected, warnings);
    }

    /// <inheritdoc/>
    public async Task<MergeCommitResponse> CommitMergeAsync(MergeCommitRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var decreeKind = Required(request.DecreeKind, "نوع المرجع مطلوب", 100);
        var decreeNumber = Required(request.DecreeNumber, "رقم المرجع مطلوب", 100);
        var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرجع");
        if (decreeDate is null)
            throw new ArgumentException("تاريخ المرجع مطلوب — استخدم مثال: 1/8/2026");

        return await _tx.RunAsync(async token =>
        {
            var survivorGroup = await _entities.GetGroupAsync(request.SurvivorGroupId, token)
                ?? throw new ArgumentException("الهوية الأم الناجية غير موجودة");
            if (!survivorGroup.IsActive)
                throw new ArgumentException("الهوية الأم الناجية غير نشطة");

            if (request.AbsorbedGroupIds.Count == 0)
                throw new ArgumentException("حدد هوية أم واحدة على الأقل للدمج");

            if (request.AbsorbedGroupIds.Contains(request.SurvivorGroupId))
                throw new ArgumentException("لا يمكن دمج هوية في نفسها");

            var survivorEntries = await _entities.ListEntriesByGroupAsync(survivorGroup.Id, token);
            var activeSurvivorEntries = survivorEntries.Where(e => e.IsActive).ToList();

            if (activeSurvivorEntries.Count == 0)
                throw new ArgumentException("الهوية الأم الناجية بلا قيود نشطة");

            if (survivorEntries.Any(e => e.NeedsReview))
                throw new ArgumentException("يجب إتمام مراجعة جميع قيود الهوية الأم الناجية قبل الدمج");

            // اسم نهائي اختياري على مستوى المجموعة (7-هـ): يُطبَّق قبل ترحيل الروابط كي تزامن النصوص
            // في خطوة واحدة بنهاية المعاملة الاسمَ الأخير.
            string? previousSurvivorName = null;
            if (!string.IsNullOrWhiteSpace(request.NewCanonicalName))
            {
                var newName = Required(request.NewCanonicalName, "الاسم النهائي مطلوب", 200);
                previousSurvivorName = survivorGroup.CanonicalName;
                await EnsureCanonicalAvailableAsync(newName, survivorGroup.Id, token);
                survivorGroup.CanonicalName = newName;
            }

            var absorbedGroupsProcessed = 0;
            var entriesMigrated = 0;
            var aliasesAdded = 0;
            var totalAffectedDocs = 0;
            var affectedDocsById = new Dictionary<int, Document>();
            var branchMap = new List<object>();
            var entryTargetByAbsorbed = new Dictionary<int, int>();
            var absorbedNames = new List<string>();

            // حفظ الاسم القديم للناجي اسمًا بديلًا (حجّة قانونية) على كل قيوده النشطة،
            // كي يبقى البحث بالاسم القديم يعثر على الجهة بعد الدمج (مواءمة 7-هـ وإعادة التسمية).
            if (previousSurvivorName is not null)
            {
                var normOldSurvivor = ArabicNameNormalizer.Normalize(previousSurvivorName);
                foreach (var se in activeSurvivorEntries.Where(e => e.IsActive))
                {
                    if (!se.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normOldSurvivor))
                    {
                        se.Aliases.Add(new PublicEntityAlias
                        {
                            PublicEntityId = se.Id,
                            AliasText = previousSurvivorName,
                        });
                        aliasesAdded++;
                    }
                }
            }

            foreach (var absorbedId in request.AbsorbedGroupIds.Distinct())
            {
                var absorbedGroup = await _entities.GetGroupAsync(absorbedId, token)
                    ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
                if (!absorbedGroup.IsActive)
                    throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");
                absorbedNames.Add(absorbedGroup.CanonicalName);

                var absorbedEntries = await _entities.ListEntriesByGroupAsync(absorbedId, token);

                if (absorbedEntries.Any(e => e.NeedsReview))
                    throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{absorbedGroup.CanonicalName}» قبل الدمج");

                foreach (var ae in absorbedEntries.Where(e => e.IsActive))
                {
                    var matchedSurvivor = activeSurvivorEntries
                        .FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);

                    var targetEntry = matchedSurvivor ?? activeSurvivorEntries.First();
                    // خريطة طيّ القيد الممتصّ إلى قيد الناجي (فرعًا بفرع)، تُستخدم لاحقًا
                    // لترحيل مندوبي القيود إلى نِسَبهم الفرعية الصحيحة بدل طيّهم على أول قيد.
                    entryTargetByAbsorbed[ae.Id] = targetEntry.Id;

                    // ترحيل روابط RegistryId + الأسماء البديلة (مساعدا الطيّ المشتركان)
                    var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, token);
                    foreach (var doc in linkedDocs)
                    {
                        if (!affectedDocsById.ContainsKey(doc.Id))
                            affectedDocsById[doc.Id] = doc;
                    }

                    foreach (var doc in linkedDocs)
                        RepointEntryLinks(doc, ae.Id, targetEntry.Id);

                    // إيقاف القيد المُدمَج
                    ae.IsActive = false;

                    AddFoldAliases(targetEntry, absorbedGroup.CanonicalName, ae, ref aliasesAdded);

                    branchMap.Add(new
                    {
                        absorbedEntryId = ae.Id,
                        absorbedGov = ae.Governorate,
                        absorbedBranch = ae.BranchName,
                        targetEntryId = targetEntry.Id,
                        targetGov = targetEntry.Governorate,
                        targetBranch = targetEntry.BranchName,
                        docsAffected = linkedDocs.Count,
                    });

                    entriesMigrated++;
                }

                absorbedGroup.IsActive = false;
                absorbedGroupsProcessed++;
            }

            // مزامنة النصوص على مستوى المجموعة (تعويض): يُستبدل كل اسم قديم — أسماء الجهات
            // المُدمجة واسم الناجي السابق إن تغيّر — بالاسم النهائي عبر كل الملفات المرتبطة،
            // بما فيها الملفات المربوطة بقيود الناجي نفسها (مواءمة 7-هـ و7-و).
            var mergeTargetName = survivorGroup.CanonicalName;
            var namesToSync = absorbedNames
                .Append(previousSurvivorName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .GroupBy(n => ArabicNameNormalizer.Normalize(n!))
                .Select(g => g.First()!)
                .ToList();
            var nameMatchedDocs = new Dictionary<int, Document>();
            foreach (var oldName in namesToSync)
            {
                var matched = await SyncTextsAfterRenameAsync(oldName!, mergeTargetName, actor.Name, token);
                foreach (var doc in matched)
                    nameMatchedDocs[doc.Id] = doc;
            }

            // عدد الملفات المتأثرة = اتحاد المترحلة عبر RegistryId والمُلتقطة بالمزامنة الاسمية،
            // ليوحّد العداد مع السلوك في UnifyNamesAsync بدل اقتِصاره على المترحلة فقط (اتساق 7-و).
            totalAffectedDocs = CountUniqueDocuments(nameMatchedDocs, affectedDocsById);

            // أعد بناء نصوص المستندات المتأثرة بالترحيل (RegistryId) التي لم تُلتقط بالمزامنة الاسمية.
            var affectedDocs = affectedDocsById.Values.ToList();
            if (affectedDocs.Count > 0)
            {
                await SyncTextsAfterFoldAsync(affectedDocs, actor.Name, token);
            }

            // مزامنة لقطات أطراف الاستئنافات عبر اتحاد الملفات المتأثرة (الاسمية + المترحلة).
            // تُطابق صور الجهة العامة داخل اللقطة عبر (Kind, PartyId) ومعرّف صف الوصلة بالملف،
            // فتلتقط حتى الصور المسماة بخلاف الاسم المعياري. يبني الدالة خريطة أسماء الصفوف
            // الحالية فيغدو التحديث مستقرًا (idempotent) مهما اختلف مسار التقاط الملف.
            var allAffectedDocs = nameMatchedDocs.Values
                .Concat(affectedDocsById.Values)
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .ToList();
            if (allAffectedDocs.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(allAffectedDocs, actor, token);

            // ترحيل مندوبي الجهات المُدمجة إلى الناجية (مواءمة 7-ز):
            // المطابقة الفرعية عبر خريطة الطيّ، والارتكاز على أول قيد ناجٍ عند غياب المطابق.
            var absorbedIdsSet = new HashSet<int>(request.AbsorbedGroupIds.Distinct());
            await MigrateDelegatesAsync(
                absorbedIdsSet, survivorGroup.Id,
                activeSurvivorEntries.FirstOrDefault()?.Id, entryTargetByAbsorbed, token);

            // حدث الدمج الأب
            var renamedSurvivor = previousSurvivorName is not null;
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                survivorGroupId = survivorGroup.Id,
                survivorGroup = survivorGroup.CanonicalName,
                oldCanonicalNames = absorbedNames,
                absorbedGroupIds = request.AbsorbedGroupIds,
                newCanonical = survivorGroup.CanonicalName,
                renamedSurvivor,
                entriesMigrated,
                aliasesAdded,
                totalAffectedDocs,
                branchMap,
                unifyTexts = request.UnifyTexts,
                decreeKind,
                decreeNumber,
                decreeDate = decreeDate!.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = survivorGroup.Id,
                ActionKind = ActionKindCatalog.Merge,
                DecreeKind = decreeKind,
                DecreeNumber = decreeNumber,
                DecreeDate = decreeDate,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);

            // وقوعات آلية لكل ملف متأثر (اتحاد الاسمية + المترحلة عبر RegistryId)
            var absorbedNamesJoined = string.Join('،', absorbedNames);
            foreach (var docId in allAffectedDocs.Select(d => d.Id))
            {
                var occurrence = new DocumentOccurrence
                {
                    DocumentId = docId,
                    OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                    EventDate = DateTime.UtcNow,
                    CreatedById = actor.UserId,
                    Details = EntityChangeMessages.MergeOccurrence(absorbedNamesJoined, survivorGroup.CanonicalName, decreeKind, decreeNumber, decreeDate),
                };
                await _occurrences.AddAsync(occurrence, token);
            }

            // تنبيه عام لكل المحامين + تنبيه خاص لرؤساء الأقسام
            await BroadcastEntityChangeToAllLawyersAsync(
                EntityChangeMessages.MergeLawyersAlert(absorbedNamesJoined, survivorGroup.CanonicalName, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);
            await BroadcastToAllHeadsAsync(
                EntityChangeMessages.MergeHeadsAlert(absorbedNamesJoined, survivorGroup.CanonicalName, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "merge_entity_registry",
                documentId: null, documentType: null,
                details: $"دمج {absorbedGroupsProcessed} هويات أم في «{survivorGroup.CanonicalName}» بموجب {BuildDecreeSuffix(decreeKind, decreeNumber, decreeDate)} — {entriesMigrated} قيد، {totalAffectedDocs} ملفًا متأثرًا",
                ct: token);

            return new MergeCommitResponse(absorbedGroupsProcessed, entriesMigrated, aliasesAdded, totalAffectedDocs, changeEvent.Id);
        }, ct);
    }

    // ── توحيد التسمية N←1 (المدير/المشرف — بلا هجرة ملفات) ──

    /// <inheritdoc/>
    public async Task<UnifyNamesPreviewResponse> PreviewUnifyAsync(UnifyNamesPreviewRequest request, CancellationToken ct = default)
    {
        var targetGroup = await _entities.GetGroupAsync(request.TargetGroupId, ct)
            ?? throw new ArgumentException("الهوية الأم الهدف غير موجودة");
        if (!targetGroup.IsActive)
            throw new ArgumentException("الهوية الأم الهدف غير نشطة");

        if (request.AbsorbedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للتوحيد");

        if (request.AbsorbedGroupIds.Contains(request.TargetGroupId))
            throw new ArgumentException("لا يمكن توحيد هوية مع نفسها");

        var targetEntries = await _entities.ListEntriesByGroupAsync(targetGroup.Id, ct);
        var activeTarget = targetEntries.Where(e => e.IsActive).ToList();
        // بركة الناجين: محاكاة مطابقة لحلقة التنفيذ في UnifyNamesAsync — تبدأ بنسخ القيود
        // النشطة للهدف ويُلحق بها كل قيد يُنقل؛ المطابقة خام (Ordinal) على (المحافظة/الفرع).
        var survivorPool = new List<PublicEntity>(activeTarget);

        var warnings = new List<string>();
        var absorbedDtos = new List<AbsorbedGroupUnifyPreviewDto>();
        var foldEntries = new List<(int GroupId, string GroupName, PublicEntity Entry)>();
        int totalToMove = 0;

        foreach (var ae in targetEntries.Where(e => e.NeedsReview))
            warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{targetGroup.CanonicalName}» (الهدف) بانتظار المراجعة");

        foreach (var absorbedId in request.AbsorbedGroupIds.Distinct())
        {
            var absorbedGroup = await _entities.GetGroupAsync(absorbedId, ct)
                ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
            if (!absorbedGroup.IsActive)
                throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");

            var absorbedEntries = await _entities.ListEntriesByGroupAsync(absorbedId, ct);
            foreach (var ae in absorbedEntries.Where(e => e.NeedsReview))
                warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{absorbedGroup.CanonicalName}» بانتظار المراجعة");

            if (!string.Equals(absorbedGroup.EntityType, targetGroup.EntityType, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"تنبيه: نوع الجهة مختلف — «{absorbedGroup.CanonicalName}» ({absorbedGroup.EntityType}) و«{targetGroup.CanonicalName}» ({targetGroup.EntityType})");

            var activeAbsorbed = absorbedEntries.Where(e => e.IsActive).ToList();

            foreach (var ae in activeAbsorbed)
            {
                var survivor = survivorPool.FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);
                if (survivor is not null)
                {
                    // الطي لا يُعدّ نقلًا: يُبطل القيد المطابق ويُرحّل روابطه إلى الناجي.
                    foldEntries.Add((absorbedGroup.Id, absorbedGroup.CanonicalName, ae));
                }
                else
                {
                    totalToMove++;
                    survivorPool.Add(ae);
                }
            }

            var govs = activeAbsorbed.Select(e => e.Governorate).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            absorbedDtos.Add(new AbsorbedGroupUnifyPreviewDto(absorbedGroup.Id, absorbedGroup.CanonicalName, activeAbsorbed.Count, govs));
        }

        // عدّ ملفات القيود المزمع طيّها دفعة واحدة ثم نبني تفاصيل الطي بالترتيب.
        var linkedCounts = await _entities.CountLinkedDocumentsByEntryIdsAsync(foldEntries.Select(f => f.Entry.Id).ToList(), ct);
        var folds = foldEntries.Select(f => new EntryFoldPreviewDto(
            f.GroupId,
            f.GroupName,
            f.Entry.Governorate,
            f.Entry.BranchName,
            linkedCounts.TryGetValue(f.Entry.Id, out var count) ? count : 0)).ToList();

        return new UnifyNamesPreviewResponse(targetGroup.CanonicalName, absorbedDtos, totalToMove, folds.Count, folds, warnings);
    }

    /// <inheritdoc/>
    public async Task<UnifyNamesResponse> UnifyNamesAsync(UnifyNamesRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        return await _tx.RunAsync(async token =>
        {
            var targetGroup = await _entities.GetGroupAsync(request.TargetGroupId, token)
                ?? throw new ArgumentException("الهوية الأم الهدف غير موجودة");
            if (!targetGroup.IsActive)
                throw new ArgumentException("الهوية الأم الهدف غير نشطة");

            if (request.AbsorbedGroupIds.Count == 0)
                throw new ArgumentException("حدد هوية أم واحدة على الأقل للتوحيد");

            if (request.AbsorbedGroupIds.Contains(request.TargetGroupId))
                throw new ArgumentException("لا يمكن توحيد هوية مع نفسها");

            var targetEntries = await _entities.ListEntriesByGroupAsync(targetGroup.Id, token);
            var activeTarget = targetEntries.Where(e => e.IsActive).ToList();
            if (targetEntries.Any(e => e.NeedsReview))
                throw new ArgumentException("يجب إتمام مراجعة جميع قيود الهوية الهدف قبل التوحيد");

            // بركة الناجين: تبدأ بنسخ القيود النشطة للهدف ويُلحق بها كل قيد يُنقل؛
            // مطابقة خام (Ordinal) على (المحافظة/الفرع) كسيمانتك الدمج والحرّاس —
            // فرق فراغ زائد يعني «نقلًا» لا «طيًّا» (سياسة المفتاح المعتمدة).
            var survivorPool = new List<PublicEntity>(activeTarget);

            // مرسوم التوحيد العام (اختياري)
            var decreeKind = NormalizeOptional(request.DecreeKind);
            var decreeNumber = NormalizeOptional(request.DecreeNumber);
            var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرسوم");
            if (decreeKind is not null && decreeKind.Length > 100)
                throw new ArgumentException("نوع المرسوم أطول من 100 حرف");
            if (decreeNumber is not null && decreeNumber.Length > 100)
                throw new ArgumentException("رقم المرسوم أطول من 100 حرف");

            int groupsUnified = 0;
            int entriesMoved = 0;
            int entriesFolded = 0;
            int aliasesAdded = 0;
            int totalAffectedDocs = 0;
            var oldNames = new List<string>();
            var movedEntryIds = new List<int>();
            var affectedDocsById = new Dictionary<int, Document>();
            var entryTargetByAbsorbed = new Dictionary<int, int>();
            var folds = new List<object>();
            var absorbedIdsDistinct = request.AbsorbedGroupIds.Distinct().ToList();

            foreach (var absorbedId in absorbedIdsDistinct)
            {
                var absorbedGroup = await _entities.GetGroupAsync(absorbedId, token)
                    ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
                if (!absorbedGroup.IsActive)
                    throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");

                var absorbedEntries = await _entities.ListEntriesByGroupAsync(absorbedId, token);
                if (absorbedEntries.Any(e => e.NeedsReview))
                    throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{absorbedGroup.CanonicalName}» قبل التوحيد");

                var activeAbsorbed = absorbedEntries.Where(e => e.IsActive).ToList();

                oldNames.Add(absorbedGroup.CanonicalName);

                foreach (var ae in activeAbsorbed)
                {
                    var survivor = survivorPool.FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);

                    if (survivor is not null)
                    {
                        // طيّ: قيد مطابق (محافظة/فرع) حرفيًا داخل الهوية الموحّدة — يُبطل ويُرحّل
                        // روابطه وأسماءه البديلة إلى الناجي (سيمانتك دمج الفروع؛ بلا fallback من نوع «أول قيد»).
                        var linkedFoldDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, token);
                        foreach (var doc in linkedFoldDocs)
                        {
                            if (!affectedDocsById.ContainsKey(doc.Id))
                                affectedDocsById[doc.Id] = doc;
                        }
                        foreach (var doc in linkedFoldDocs)
                            RepointEntryLinks(doc, ae.Id, survivor.Id);

                        ae.IsActive = false;
                        entryTargetByAbsorbed[ae.Id] = survivor.Id;
                        AddFoldAliases(survivor, absorbedGroup.CanonicalName, ae, ref aliasesAdded);
                        entriesFolded++;
                        folds.Add(new
                        {
                            absorbedEntryId = ae.Id,
                            targetEntryId = survivor.Id,
                            governorate = ae.Governorate,
                            branchName = ae.BranchName,
                            linkedDocs = linkedFoldDocs.Count,
                        });
                    }
                    else
                    {
                        // نقل القيد إلى مجموعة الهدف (كما هو)
                        ae.GroupId = targetGroup.Id;
                        survivorPool.Add(ae);
                        movedEntryIds.Add(ae.Id);
                        entriesMoved++;

                        // حفظ الاسم الممتصّ اسمًا بديلًا «للبحث فقط» على القيد المنقول
                        var normAbsorbed = ArabicNameNormalizer.Normalize(absorbedGroup.CanonicalName);
                        if (!ae.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normAbsorbed))
                        {
                            ae.Aliases.Add(new PublicEntityAlias
                            {
                                PublicEntityId = ae.Id,
                                AliasText = absorbedGroup.CanonicalName,
                            });
                            aliasesAdded++;
                        }
                    }
                }

                absorbedGroup.IsActive = false;
                groupsUnified++;
            }

            // 4) مزامنة النصوص في الملفات المرتبطة بالقيود المنقولة (كل اسم ممتصّ ← الاسم الموحّد)
            //    تُجدّد صور الأسماء القديمة لتصبح التسمية الموحدة فقط في كل الملفات والاستئنافات.
            var nameMatchedDocs = new Dictionary<int, Document>();
            foreach (var oldName in oldNames)
            {
                var matched = await SyncTextsAfterRenameAsync(oldName, targetGroup.CanonicalName, actor.Name, token);
                foreach (var doc in matched)
                    nameMatchedDocs[doc.Id] = doc;
            }

            // الملفات المربوطة عبر RegistryId بالقيود المنقولة — أعِد بناء نصوصها لتتقيد بالاسم الموحّد
            // إن لم تلتقطها المزامنة الاسمية (مثل مسمّاة بخلاف الاسم المعياري).
            foreach (var movedId in movedEntryIds)
            {
                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(movedId, token);
                foreach (var doc in linkedDocs)
                    affectedDocsById[doc.Id] = doc;
            }

            var affectedDocs = affectedDocsById.Values.ToList();
            if (affectedDocs.Count > 0)
                await SyncTextsAfterFoldAsync(affectedDocs, actor.Name, token);

            totalAffectedDocs = CountUniqueDocuments(nameMatchedDocs, affectedDocsById);

            // 5) مزامنة لقطات أطراف الاستئنافات عبر اتحاد الملفات المتأثرة (الاسمية + المترحلة)
            var allAffectedDocs = nameMatchedDocs.Values
                .Concat(affectedDocsById.Values)
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .ToList();
            if (allAffectedDocs.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(allAffectedDocs, actor, token);

            // 6) ترحيل مندوبي الجهات الممتصة إلى الهدف (على مستوى المجموعة)، مع خريطة الطيّ:
            //    المطوي يرحل لناجيه، والمنقول (غير المُدرج) يبقى على قيده.
            var absorbedIdsSet = new HashSet<int>(absorbedIdsDistinct);
            await MigrateDelegatesAsync(absorbedIdsSet, targetGroup.Id, null, entryTargetByAbsorbed, token);

            // 7) سجل التغيير
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                targetGroupId = targetGroup.Id,
                targetGroup = targetGroup.CanonicalName,
                absorbedGroupIds = absorbedIdsDistinct,
                oldCanonicalNames = oldNames,
                entriesMoved,
                entriesFolded,
                folds,
                groupsUnified,
                aliasesAdded,
                totalAffectedDocs,
                decreeKind,
                decreeNumber,
                decreeDate = decreeDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            });

            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = targetGroup.Id,
                ActionKind = ActionKindCatalog.Unify,
                DecreeKind = decreeKind,
                DecreeNumber = decreeNumber,
                DecreeDate = decreeDate,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);

            // 8) وقوعات آلية لكل ملف متأثر (نوع entity-change)
            var absorbedNamesJoined = string.Join('،', oldNames);
            foreach (var docId in allAffectedDocs.Select(d => d.Id))
            {
                var occurrence = new DocumentOccurrence
                {
                    DocumentId = docId,
                    OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                    EventDate = DateTime.UtcNow,
                    CreatedById = actor.UserId,
                    Details = EntityChangeMessages.UnifyOccurrence(absorbedNamesJoined, targetGroup.CanonicalName, decreeKind ?? "", decreeNumber ?? "", decreeDate),
                };
                await _occurrences.AddAsync(occurrence, token);
            }

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "unify_entity_names",
                documentId: null, documentType: null,
                details: $"توحيد تسمية {groupsUnified} هويات في «{targetGroup.CanonicalName}» — {entriesMoved} قيدًا نُقل، {entriesFolded} قيدًا طُوي، {totalAffectedDocs} ملفًا متأثرًا",
                ct: token);

            // تنبيه عام لكل المحامين + تنبيه خاص لرؤساء الأقسام
            await BroadcastEntityChangeToAllLawyersAsync(
                EntityChangeMessages.UnifyLawyersAlert(absorbedNamesJoined, targetGroup.CanonicalName, decreeKind ?? "", decreeNumber ?? "", decreeDate),
                actor.UserId, token);
            await BroadcastToAllHeadsAsync(
                EntityChangeMessages.UnifyHeadsAlert(absorbedNamesJoined, targetGroup.CanonicalName, decreeKind ?? "", decreeNumber ?? "", decreeDate),
                actor.UserId, token);

            return new UnifyNamesResponse(targetGroup.Id, targetGroup.CanonicalName, groupsUnified, entriesMoved, entriesFolded, changeEvent.Id);
        }, ct);
    }

    private static int CountUniqueDocuments(Dictionary<int, Document> a, Dictionary<int, Document> b)
    {
        var ids = new HashSet<int>();
        foreach (var k in a.Keys) ids.Add(k);
        foreach (var k in b.Keys) ids.Add(k);
        return ids.Count;
    }

    /// <summary>مزامنة نصوص الملف بعد الطيّ ( Collector for applicant+executed).</summary>
    private async Task SyncTextsAfterFoldAsync(List<Document> linkedDocs, string? actorName, CancellationToken token)
    {
        if (linkedDocs.Count == 0) return;

        foreach (var doc in linkedDocs)
        {
            var rebuilt = ApplicantTextBuilder.Build(doc.ApplicantPublicEntities);
            if (!string.IsNullOrWhiteSpace(rebuilt) || string.IsNullOrWhiteSpace(doc.Applicant))
                doc.Applicant = rebuilt;
            // ملف «منفذ عليه»/«عرض وايداع» بلا جهة طالبة كلاسية: اسم الطالب يُشتق من
            // طلبات التنفيذ الاعتباريين المربوطين جهة عامة وأسماء طلبات العرض الطبيعية
            // فيتطابق العنوان مع الاسم المعياري بعد الطيّ/الدمج/الحلول.
            if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide)
                && doc.ApplicantPublicEntities.Count == 0)
            {
                var executedApplicantName = doc.ExecutionApplicants
                    .Select(a => (a.Name ?? string.Empty).Trim())
                    .FirstOrDefault(v => v.Length > 0);
                doc.Applicant = executedApplicantName ?? doc.Applicant;
            }
            doc.SearchText = DocumentSearchTextBuilder.Build(doc);
            doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
        }
        await _uow.SaveChangesAsync(token);
    }

    /// <summary>
    /// مزامنة لقطات أطراف الاستئنافات (AppellantsJson / AppelleesJson) بعد تغيير جهة عامة
    /// (إعادة تسمية / دمج / حلول) على مجموعة من الملفات المتأثرة. تُطابق صور الجهة العامة
    /// (طالب أو منفذ عليه) داخل اللقطات عبر (Kind, PartyId) ومعرّف صف الوصلة بالملف — لا عبر
    /// الاسم حصرًا — فتلتقط حتى الصور المخزَّنة بأسامٍ تختلف عن الاسم المعياري (الثغرة)،
    /// وتُجدّد اسم الصورة من الاسم الحالي للصف المرتَّل. تعمل داخل معاملة المتصل وتُدوّن
    /// التغيير عبر AuditLogger لكل استئناف.
    /// </summary>
    private async Task SyncAppealsAfterEntityChangeAsync(
        IReadOnlyCollection<Document> affectedDocs,
        EntityRegistryActor actor,
        CancellationToken token)
    {
        if (affectedDocs.Count == 0)
            return;

        // خريطة (Kind, PartyId) → الاسم الحالي لصف الوصلة، من الملفات المتأثرة
        // (حُرِّرت أسماءها قبلاً بمزامنة النصوص أو بالتحديث المباشر عند الحلول).
        var newNames = new Dictionary<(string Kind, int PartyId), string>();
        foreach (var doc in affectedDocs)
        {
            foreach (var a in doc.ApplicantPublicEntities)
                if (!string.IsNullOrWhiteSpace(a.Name))
                    newNames[("applicant-entity", a.Id)] = a.Name;
            foreach (var e in doc.ExecutedPublicEntities)
                if (!string.IsNullOrWhiteSpace(e.EntityName))
                    newNames[("executed-public", e.Id)] = e.EntityName;
            // طالب التنفيذ الاعتباري المربوط جهة عامة (RegistryId != null): الاسم الاعتباري
            // يعادل TripleOr(Name, null, null, null) == Name — لا يُلمس natural (بلا RegistryId).
            foreach (var ea in doc.ExecutionApplicants.Where(ea => ea.RegistryId.HasValue))
                if (!string.IsNullOrWhiteSpace(ea.Name))
                    newNames[("execution-applicant", ea.Id)] = ea.Name;
        }
        if (newNames.Count == 0)
            return;

        var documentIds = affectedDocs.Select(d => d.Id).Distinct().ToList();
        var appeals = await _appeals.ListByDocumentIdsAsync(documentIds, token);
        if (appeals.Count == 0)
            return;

        foreach (var appeal in appeals)
        {
            var newAppellants = AppealSnapshotSerializer.UpdateEntityParties(appeal.AppellantsJson, newNames);
            var newAppellees = AppealSnapshotSerializer.UpdateEntityParties(appeal.AppelleesJson, newNames);
            var changed = !string.Equals(newAppellants, appeal.AppellantsJson, StringComparison.Ordinal)
                          || !string.Equals(newAppellees, appeal.AppelleesJson, StringComparison.Ordinal);
            if (!changed)
                continue;

            appeal.AppellantsJson = newAppellants;
            appeal.AppelleesJson = newAppellees;
            appeal.UpdatedAt = DateTime.UtcNow;
            await _audit.LogAsync(actor.Name, "appeal_entity_sync",
                documentId: appeal.DocumentId, documentType: null,
                details: $"مزامنة لقطات الاستئناف بعد تغيير جهة عامة في الملف #{appeal.DocumentId}",
                ct: token);
        }

        await _uow.SaveChangesAsync(token);
    }

    /// <summary>ترحيل مندوبي الجهات المُمتصة/المُلغاة إلى الهوية الهدف (مواءمة 7-ز).</summary>
    /// <remarks>
    /// المندوب المجموعتي يُتوجَّه دائمًا إلى المجموعة الهدف. المندوب القيدي يُتوجَّه إلى
    /// القيد المطابق لفرعه عبر <paramref name="entryTargetByAbsorbedEntry"/>، ويسقط على
    /// <paramref name="defaultTargetEntryId"/> عند غياب المطابق (حيث لا تُمرَّر الخريطة).
    /// في مسار التوحيد تُمرَّر خريطة الطيّ <paramref name="entryTargetByAbsorbedEntry"/>
    /// ليرحل المطوي إلى قرينه الناجي، بينما المُتَنقَّل (غير المُدرج في الخريطة) يبقى على
    /// قيده دون تغيير، مع تمرير null لقيمة <paramref name="defaultTargetEntryId"/> كي لا
    /// يُطوى المنقول على قيد عشوائي.
    /// </remarks>
    private async Task<int> MigrateDelegatesAsync(
        HashSet<int> absorbedIds,
        int targetGroupId,
        int? defaultTargetEntryId,
        IReadOnlyDictionary<int, int>? entryTargetByAbsorbedEntry,
        CancellationToken token)
    {
        var delegates = await _users.ListEntityManagersByGroupIdsAsync(absorbedIds, token);
        foreach (var delegateUser in delegates)
        {
            if (delegateUser.PortalGroupId.HasValue && absorbedIds.Contains(delegateUser.PortalGroupId.Value))
                delegateUser.PortalGroupId = targetGroupId;
            if (delegateUser.PortalEntryId.HasValue
                && delegateUser.PortalEntry is not null
                && absorbedIds.Contains(delegateUser.PortalEntry.GroupId))
            {
                delegateUser.PortalGroupId = targetGroupId;
                var branchTarget = entryTargetByAbsorbedEntry is not null
                    && entryTargetByAbsorbedEntry.TryGetValue(delegateUser.PortalEntryId.Value, out var matched)
                        ? matched
                        : defaultTargetEntryId;
                if (branchTarget.HasValue)
                    delegateUser.PortalEntryId = branchTarget.Value;
            }
        }
        return delegates.Count;
    }

    private static string JoinNameBranch(string? name, string? branch)
        => string.Join(' ', new[] { name, branch }.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static PublicEntityEntryDto ToEntryDto(PublicEntityGroup group, PublicEntity entry) => new(
        entry.Id,
        group.Id,
        group.CanonicalName,
        group.EntityType,
        entry.Governorate,
        entry.BranchName,
        entry.CitationFormula,
        entry.Status,
        entry.IsActive,
        entry.CreatedAt,
        entry.Aliases.Select(a => a.AliasText).ToList(),
        entry.CreatedBy?.FullName,
        entry.NeedsReview,
        entry.CoverageLabel,
        entry.IsParentEntity);

    // ── أداة إعادة تسمية الهوية الأم (المدير/المشرف — مستوى المجموعة) ──
    // القاعدة: إعادة التسمية على مستوى المجموعة لا تمس Governorate/BranchName/CitationFormula/CoverageLabel
    // — تغيّر Group.CanonicalName فقط، وتُحفظ الأسماء القديمة أسماءً بديلة (حجّة قانونية د5).

    /// <inheritdoc/>
    public async Task<RenameGroupPreviewResponse> PreviewRenameGroupAsync(
        RenameGroupPreviewRequest request, CancellationToken ct = default)
    {
        var group = await _entities.GetGroupAsync(request.GroupId, ct)
            ?? throw new ArgumentException("الهوية الأم غير موجودة");
        var newName = Required(request.NewCanonicalName, "اسم الجهة مطلوب", 200);
        if (group.Entries.Any(e => e.NeedsReview))
            throw new ArgumentException("يجب إتمام مراجعة جميع قيود الهوية الأم قبل إعادة تسميتها");

        var affected = await CountDocumentsForGroupAsync(request.GroupId, ct);
        var branches = await BranchNamesForGroupAsync(request.GroupId, ct);
        return new RenameGroupPreviewResponse(group.CanonicalName, newName, affected, branches);
    }

    /// <inheritdoc/>
    public async Task<RenameGroupResponse> RenameGroupAsync(
        RenameGroupRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        if (!RolePermissions_IsFullAccess(actor.Role))
            throw new UnauthorizedAccessException("إعادة التسمية للمدير أو المشرف فقط");

        var newCanonical = Required(request.NewCanonicalName, "اسم الجهة مطلوب", 200);
        var decreeKind = Required(request.DecreeKind, "نوع المرجع مطلوب", 100);
        var decreeNumber = Required(request.DecreeNumber, "رقم المرجع مطلوب", 100);
        var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرجع");
        if (decreeDate is null)
            throw new ArgumentException("تاريخ المرجع مطلوب — استخدم مثال: 1/8/2026");

        return await _tx.RunAsync(async token =>
        {
            var group = await _entities.GetGroupAsync(request.GroupId, token)
                ?? throw new ArgumentException("الهوية الأم غير موجودة");
            if (!group.IsActive)
                throw new ArgumentException("الهوية الأم غير نشطة");
            if (group.Entries.Any(e => e.NeedsReview))
                throw new ArgumentException("يجب إتمام مراجعة جميع قيود الهوية الأم قبل إعادة تسميتها");

            var oldCanonical = group.CanonicalName;
            if (string.Equals(ArabicNameNormalizer.Normalize(oldCanonical),
                ArabicNameNormalizer.Normalize(newCanonical), StringComparison.Ordinal))
                throw new ArgumentException("الاسم الجديد مطابق للاسم الحالي");

            await EnsureCanonicalAvailableAsync(newCanonical, group.Id, token);

            group.CanonicalName = newCanonical;

            // حفظ الاسم القديم اسمًا بديلًا (حجّة قانونية): يُضاف على القيد الأم بمحافظة الفرع
            // وعلى كل قيود المجموعة ليبقى البحث بالاسم القديم يعثر على الجهة.
            var entries = await _entities.ListEntriesByGroupAsync(group.Id, token);
            foreach (var entry in entries.Where(e => e.IsActive))
            {
                var normOld = ArabicNameNormalizer.Normalize(oldCanonical);
                if (!entry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normOld))
                {
                    entry.Aliases.Add(new PublicEntityAlias
                    {
                        PublicEntityId = entry.Id,
                        AliasText = oldCanonical,
                    });
                }
            }

            // مزامنة النصوص على مستوى المجموعة (الاسم القديم ← الجديد عبر كل الملفات المرتبطة)
            var affectedDocs = await SyncTextsAfterRenameAsync(oldCanonical, newCanonical, actor.Name, token);
            var affected = affectedDocs.Count;

            // مزامنة لقطات أطراف الاستئنافات المرتبطة بنفس الملفات المتأثرة
            await SyncAppealsAfterEntityChangeAsync(affectedDocs, actor, token);

            // سجل التغيير
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                oldCanonicalNames = new[] { oldCanonical },
                newCanonical = group.CanonicalName,
                entityType = group.EntityType,
                decreeKind,
                decreeNumber,
                decreeDate = decreeDate!.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                affectedDocuments = affected,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = group.Id,
                ActionKind = ActionKindCatalog.Rename,
                DecreeKind = decreeKind,
                DecreeNumber = decreeNumber,
                DecreeDate = decreeDate,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);

            await _uow.SaveChangesAsync(token);

            // وقوعات آلية لكل ملف متأثر (المزامنة الاسمية الفعلية — لا إعادة استعلام عبر RegistryId)
            foreach (var docId in affectedDocs.Select(d => d.Id))
            {
                var occurrence = new DocumentOccurrence
                {
                    DocumentId = docId,
                    OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                    EventDate = DateTime.UtcNow,
                    CreatedById = actor.UserId,
                    Details = EntityChangeMessages.RenameOccurrence(oldCanonical, group.CanonicalName, decreeKind, decreeNumber, decreeDate),
                };
                await _occurrences.AddAsync(occurrence, token);
            }

            // تنبيه عام لكل المحامين + تنبيه خاص لرؤساء الأقسام
            await BroadcastEntityChangeToAllLawyersAsync(
                EntityChangeMessages.RenameLawyersAlert(oldCanonical, group.CanonicalName, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);
            await BroadcastToAllHeadsAsync(
                EntityChangeMessages.RenameHeadsAlert(oldCanonical, group.CanonicalName, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "rename_public_entity_group",
                documentId: null, documentType: null,
                details: $"أعاد تسمية الهوية الأم: «{oldCanonical}» إلى «{group.CanonicalName}» بموجب {BuildDecreeSuffix(decreeKind, decreeNumber, decreeDate)} — {affected} ملفًا متأثرًا",
                ct: token);

            return new RenameGroupResponse(group.Id, oldCanonical, group.CanonicalName, affected, changeEvent.Id);
        }, ct);
    }

    // ── أداة الحلول (إلغاء عدة هويات أم واستبدالها بهوية جديدة) ──

    /// <inheritdoc/>
    public async Task<AbolishReplacePreviewResponse> PreviewAbolishAndReplaceAsync(
        AbolishReplacePreviewRequest request, CancellationToken ct = default)
    {
        if (request.AbolishedGroupIds is null || request.AbolishedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للإلغاء");

        var names = new List<string>();
        var affectedDocs = 0;
        var branches = new HashSet<string>(StringComparer.Ordinal);
        var abolishedGroupIds = new HashSet<int>(request.AbolishedGroupIds);

        foreach (var id in request.AbolishedGroupIds.Distinct())
        {
            var group = await _entities.GetGroupAsync(id, ct)
                ?? throw new ArgumentException($"الهوية الأم #{id} غير موجودة");
            if (!group.IsActive)
                throw new ArgumentException($"الهوية الأم «{group.CanonicalName}» غير نشطة");
            if (group.Entries.Any(e => e.NeedsReview))
                throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{group.CanonicalName}» قبل الإلغاء");
            names.Add(group.CanonicalName);
            affectedDocs += await CountDocumentsForGroupAsync(id, ct);
            foreach (var b in await BranchNamesForGroupAsync(id, ct))
                branches.Add(b);
        }

        var delegates = await _users.ListEntityManagersByGroupIdsAsync(abolishedGroupIds, ct);
        return new AbolishReplacePreviewResponse(
            names, request.AbolishedGroupIds.Count,
            await CountActiveEntriesForGroupsAsync(abolishedGroupIds, ct),
            affectedDocs, delegates.Count, branches.OrderBy(x => x).ToList());
    }

    /// <inheritdoc/>
    public async Task<AbolishAndReplaceResponse> AbolishAndReplaceAsync(
        AbolishAndReplaceRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        if (!RolePermissions_IsFullAccess(actor.Role))
            throw new UnauthorizedAccessException("الحلول (إلغاء واستبدال) للمدير أو المشرف فقط");
        if (request.AbolishedGroupIds is null || request.AbolishedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للإلغاء");

        var newCanonical = Required(request.NewCanonicalName, "اسم الجهة مطلوب", 200);
        var entityType = ValidEntityType(request.EntityType);
        var governorate = Required(request.Governorate, "المحافظة مطلوبة", 100);
        var citationFormula = ValidCitationFormula(request.CitationFormula, CitationFormulaCatalog.AddToJob);
        var coverageLabel = ValidateCoverageLabel(request.CoverageLabel);
        var decreeKind = Required(request.DecreeKind, "نوع المرجع مطلوب", 100);
        var decreeNumber = Required(request.DecreeNumber, "رقم المرجع مطلوب", 100);
        var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرجع");
        if (decreeDate is null)
            throw new ArgumentException("تاريخ المرجع مطلوب — استخدم مثال: 1/8/2026");

        var abolishedIds = request.AbolishedGroupIds.Distinct().ToList();

        return await _tx.RunAsync(async token =>
        {
            // 1) تحقق: كل الجهات المُلغاة نشطة بلا NeedsReview؛ الاسم الجديد فريد
            foreach (var id in abolishedIds)
            {
                var g = await _entities.GetGroupAsync(id, token)
                    ?? throw new ArgumentException($"الهوية الأم #{id} غير موجودة");
                if (!g.IsActive)
                    throw new ArgumentException($"الهوية الأم «{g.CanonicalName}» غير نشطة");
                if (g.Entries.Any(e => e.NeedsReview))
                    throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{g.CanonicalName}» قبل الإلغاء");
            }
            await EnsureCanonicalAvailableAsync(newCanonical, 0, token);

            // 2) إنشاء الهوية الأم الجديدة + قيدها الأم
            var newGroup = new PublicEntityGroup
            {
                CanonicalName = newCanonical,
                EntityType = entityType,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            await _entities.AddGroupAsync(newGroup, token);
            await _uow.SaveChangesAsync(token);

            var newParentEntry = new PublicEntity
            {
                GroupId = newGroup.Id,
                Group = newGroup,
                Governorate = governorate,
                BranchName = DefaultBranchName,
                IsParentEntity = true,
                CoverageLabel = coverageLabel,
                CitationFormula = citationFormula,
                Status = EntityStatusCatalog.Final,
                IsActive = true,
                NeedsReview = false,
                CreatedById = actor.UserId,
                CreatedAt = DateTime.UtcNow,
            };
            var aliases = CleanAliases(request.Aliases, ArabicNameNormalizer.Normalize(newCanonical));
            foreach (var alias in aliases)
                newParentEntry.Aliases.Add(new PublicEntityAlias { PublicEntityId = newParentEntry.Id, AliasText = alias });
            await _entities.AddEntryAsync(newParentEntry, token);
            await _uow.SaveChangesAsync(token);

            // 3) ترحيل روابط القيود الفعّالة للجهات المُلغاة إلى القيد الأم الجديد + إيقافها
            var abolishedIdsSet = new HashSet<int>(abolishedIds);
            var abolishedNames = new List<string>();
            var affectedDocsById = new Dictionary<int, Document>();
            var entriesMoved = 0;

            foreach (var id in abolishedIds)
            {
                var group = await _entities.GetGroupAsync(id, token)
                    ?? throw new ArgumentException($"الهوية الأم #{id} غير موجودة");
                abolishedNames.Add(group.CanonicalName);
                var entries = await _entities.ListEntriesByGroupAsync(id, token);

                foreach (var entry in entries.Where(e => e.IsActive))
                {
                    var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, token);
                    foreach (var doc in linkedDocs)
                    {
                        foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == entry.Id))
                        {
                            a.RegistryId = newParentEntry.Id;
                            // تحديث مباشر للاسم: يُحلّ الاسم الجديد محل القديم في نص الطالب
                            // (ApplicantTextBuilder يقرأ e.Name)، فلا يبقى الاسم المُلغى في النصوص.
                            a.Name = newCanonical;
                        }
                        foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == entry.Id))
                        {
                            e.RegistryId = newParentEntry.Id;
                            e.EntityName = newCanonical;
                        }
                        foreach (var ea in doc.ExecutionApplicants.Where(ea => ea.RegistryId == entry.Id))
                        {
                            ea.RegistryId = newParentEntry.Id;
                            ea.Name = newCanonical;
                        }
                        doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);
                        if (!affectedDocsById.ContainsKey(doc.Id))
                            affectedDocsById[doc.Id] = doc;
                        await _occurrences.AddAsync(new DocumentOccurrence
                        {
                            DocumentId = doc.Id,
                            OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                            EventDate = DateTime.UtcNow,
                            CreatedById = actor.UserId,
                            Details = EntityChangeMessages.AbolishOccurrence(newCanonical, group.CanonicalName, decreeKind, decreeNumber, decreeDate),
                        }, token);
                    }

                    entry.IsActive = false;
                    entriesMoved++;
                }

                // حفظ أسماء الجهات المُلغاة أسماءً بديلة على القيد الجديد (مرة لكل مجموعة ملغاة)
                var norm = ArabicNameNormalizer.Normalize(group.CanonicalName);
                if (!newParentEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == norm))
                    newParentEntry.Aliases.Add(new PublicEntityAlias
                    {
                        PublicEntityId = newParentEntry.Id,
                        AliasText = group.CanonicalName,
                    });

                group.IsActive = false;
            }

            // 4) مزامنة النصوص للملفات المتأثرة (يحلّ الاسم الجديد محل القديم)
            if (affectedDocsById.Count > 0)
            {
                var affectedDocsList = affectedDocsById.Values.ToList();
                await SyncTextsAfterFoldAsync(affectedDocsList, actor.Name, token);

                // مزامنة لقطات أطراف الاستئنافات للملفات المتأثرة (تُطابق صور الجهة العامة
                // عبر (Kind, PartyId) فيلتقط حتى الصور المخزَّنة باسم مختلف عن الاسم المعياري).
                await SyncAppealsAfterEntityChangeAsync(affectedDocsList, actor, token);
            }
            await _uow.SaveChangesAsync(token);

            // 5) ترحيل مندوبي الجهات المُلغاة إلى الهوية الجديدة (مواءمة 7-ز)
            var delegatesCount = await MigrateDelegatesAsync(abolishedIdsSet, newGroup.Id, newParentEntry.Id, null, token);

            // 6) سجل التغيير
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                abolishedGroupIds = abolishedIds,
                oldCanonicalNames = abolishedNames,
                newCanonical = newGroup.CanonicalName,
                entityType = newGroup.EntityType,
                governorate,
                branchName = DefaultBranchName,
                entriesMoved,
                affectedDocuments = affectedDocsById.Count,
                delegatesReassigned = delegatesCount,
                decreeKind,
                decreeNumber,
                decreeDate = decreeDate!.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = newGroup.Id,
                ActionKind = ActionKindCatalog.Abolish,
                DecreeKind = decreeKind,
                DecreeNumber = decreeNumber,
                DecreeDate = decreeDate,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);

            // 8) تنبيه عام لكل المحامين + رؤساء الأقسام
            var abolishedNamesJoined = string.Join('،', abolishedNames);
            await BroadcastEntityChangeToAllLawyersAsync(
                EntityChangeMessages.AbolishLawyersAlert(newGroup.CanonicalName, abolishedNamesJoined, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);
            await BroadcastToAllHeadsAsync(
                EntityChangeMessages.AbolishHeadsAlert(newGroup.CanonicalName, abolishedNamesJoined, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "abolish_and_replace_entity",
                documentId: null, documentType: null,
                details: $"حلّت «{newGroup.CanonicalName}» محل {abolishedIds.Count} هويات، {entriesMoved} قيدًا، {affectedDocsById.Count} ملفًا متأثرًا، {delegatesCount} مندوبًا",
                ct: token);

            return new AbolishAndReplaceResponse(
                newGroup.Id, newGroup.CanonicalName, abolishedIds.Count, entriesMoved,
                affectedDocsById.Count, changeEvent.Id);
        }, ct);
    }

    // ── مساعدات البث العام لكل الفروع (أ1) ──

    /// <summary>تنبيه تعميم لكل المحامين في كل الفروع النشطة — كل تنبيه برسالة مُقصّرة عند 2000.</summary>
    /// <remarks>يُلتحم بالمعاملة الخارجية الواحدة التي يفتحها <see cref="TransactionRunner"/>، فيُثبَّت الكل أو يُتراجع الكل مع سائر التغييرات.</remarks>
    private async Task BroadcastEntityChangeToAllLawyersAsync(string message, int actorUserId, CancellationToken token)
    {
        var grouped = await _headAlerts.ListAllActiveLawyersGroupedByBranchAsync(token);
        foreach (var (branchId, lawyers) in grouped)
        {
            if (lawyers.Count == 0)
                continue;
            var alert = new HeadAlert
            {
                BranchId = branchId,
                CreatedById = actorUserId,
                TargetType = HeadAlertTargetType.Branch,
                Message = message.Length > 2000 ? message[..2000] : message,
                CreatedAt = DateTime.UtcNow,
                Recipients = { },
            };
            foreach (var lawyer in lawyers)
                alert.Recipients.Add(new HeadAlertRecipient { UserId = lawyer.Id });
            await _headAlerts.AddAsync(alert, token);
        }
    }

    /// <summary>تنبيه لكل رؤساء الأقسام في كل الفروع النشطة.</summary>
    /// <remarks>يُلتحم بالمعاملة الخارجية الواحدة التي يفتحها <see cref="TransactionRunner"/>، فيُثبَّت الكل أو يُتراجع الكل مع سائر التغييرات.</remarks>
    private async Task BroadcastToAllHeadsAsync(string message, int actorUserId, CancellationToken token)
    {
        var grouped = await _headAlerts.ListAllActiveHeadsGroupedByBranchAsync(token);
        foreach (var (branchId, heads) in grouped)
        {
            if (heads.Count == 0)
                continue;
            var alert = new HeadAlert
            {
                BranchId = branchId,
                CreatedById = actorUserId,
                TargetType = HeadAlertTargetType.Branch,
                Message = message.Length > 2000 ? message[..2000] : message,
                CreatedAt = DateTime.UtcNow,
                Recipients = { },
            };
            foreach (var head in heads)
                alert.Recipients.Add(new HeadAlertRecipient { UserId = head.Id });
            await _headAlerts.AddAsync(alert, token);
        }
    }

    // ── مساعدات مشتركة ──

    /// <summary>سقف لاحقة المرجع — يفوّض إلى <see cref="EntityChangeMessages.DecreeSuffix"/> (المصدر الموحّد).</summary>
    private static string BuildDecreeSuffix(string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.DecreeSuffix(decreeKind, decreeNumber, decreeDate);

    private async Task<int> CountDocumentsForGroupAsync(int groupId, CancellationToken token)
    {
        var entries = await _entities.ListEntriesByGroupAsync(groupId, token);
        var ids = new HashSet<int>();
        foreach (var entry in entries.Where(e => e.IsActive))
        {
            foreach (var d in await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, token))
                ids.Add(d.Id);
        }
        return ids.Count;
    }

    private async Task<List<string>> BranchNamesForGroupAsync(int groupId, CancellationToken token)
    {
        var entries = await _entities.ListEntriesByGroupAsync(groupId, token);
        var branches = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(e => e.IsActive))
        {
            if (!string.IsNullOrWhiteSpace(entry.BranchName) && entry.BranchName != DefaultBranchName)
                branches.Add(entry.BranchName);
        }
        return branches.OrderBy(x => x).ToList();
    }

    private async Task<int> CountActiveEntriesForGroupsAsync(IReadOnlyCollection<int> groupIds, CancellationToken token)
    {
        var count = 0;
        foreach (var id in groupIds)
            count += (await _entities.ListEntriesByGroupAsync(id, token)).Count(e => e.IsActive);
        return count;
    }

    private static bool RolePermissions_IsFullAccess(DocGenerator.Domain.Enums.UserRole role)
        => role is DocGenerator.Domain.Enums.UserRole.Manager or DocGenerator.Domain.Enums.UserRole.Admin;
}
