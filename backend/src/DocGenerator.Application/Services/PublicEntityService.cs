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

    /// <summary>سجل تغييرات الجهات — مصدره PublicEntityChangeEvent فقط (د5 §7).</summary>
    Task<PagedResult<EntityChangeEventDto>> ListChangeEventsAsync(EntityChangeEventQuery query, CancellationToken ct = default);

    /// <summary>تصدير سجل التغييرات إلى Excel (نفس فلاتر القائمة).</summary>
    Task<byte[]> ExportChangeEventsAsync(EntityChangeEventQuery query, CancellationToken ct = default);

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

    /// <summary>المجموعات المتشابهة (كشف Union-Find) لتبويب «المجموعات المتشابهة» في توحيد التسمية.</summary>
    Task<SimilarGroupsResponse> GetSimilarGroupsAsync(double threshold, CancellationToken ct = default);

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

        // نطاق رئيس القسم: محافظته فقط
        if (actor.Role == UserRole.Head && actor.BranchId.HasValue)
        {
            var branch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            var gov = NormalizeOptional(branch?.Governorate);
            if (gov is not null)
                filtered = filtered.Where(e => e.Governorate == gov).ToList();
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
    public async Task<SimilarGroupsResponse> GetSimilarGroupsAsync(double threshold, CancellationToken ct = default)
    {
        var t = threshold <= 0 ? ArabicNameSimilarity.DefaultClusterThreshold : Math.Clamp(threshold, 0, 1);
        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        var active = groups.Where(g => g.IsActive).ToList();

        var clusters = ArabicNameSimilarity.ClusterGroups(groups, t);
        var allClusterIds = clusters.SelectMany(c => c.Select(g => g.Id)).Distinct().ToList();
        var linkedCounts = await _entities.CountLinkedDocumentsByGroupIdsAsync(allClusterIds, ct);

        var clusterDtos = new List<SimilarGroupClusterDto>();
        int clusterIndex = 0;
        foreach (var cluster in clusters)
        {
            var items = cluster.Select(g =>
            {
                var entryCount = g.Entries.Count(e => e.IsActive);
                // متوسط التشابه لهذه الجهة تجاه باقي أفراد مجموعتها.
                double itemAvg = 0;
                if (cluster.Count >= 2)
                {
                    double sum = 0;
                    int count = 0;
                    foreach (var other in cluster)
                    {
                        if (other.Id == g.Id)
                            continue;
                        sum += ArabicNameSimilarity.Similarity(g.CanonicalName, other.CanonicalName);
                        count++;
                    }
                    itemAvg = count == 0 ? 0 : sum / count;
                }
                return new SimilarGroupItemDto(
                    g.Id,
                    g.CanonicalName,
                    g.EntityType,
                    entryCount,
                    linkedCounts.TryGetValue(g.Id, out var c) ? c : 0,
                    Math.Round(itemAvg, 3));
            }).ToList();

            // متوسط التشابه لبيئة التجمع.
            double clusterAvg = 0;
            if (cluster.Count >= 2)
            {
                double sum = 0;
                int pairCount = 0;
                for (int i = 0; i < cluster.Count; i++)
                    for (int j = i + 1; j < cluster.Count; j++)
                    {
                        sum += ArabicNameSimilarity.Similarity(cluster[i].CanonicalName, cluster[j].CanonicalName);
                        pairCount++;
                    }
                clusterAvg = pairCount == 0 ? 0 : sum / pairCount;
            }

            clusterDtos.Add(new SimilarGroupClusterDto(++clusterIndex, Math.Round(clusterAvg, 3), items));
        }

        return new SimilarGroupsResponse(clusterDtos, active.Count, t);
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
        if (request.IsParentEntity is bool isParent)
            entry.IsParentEntity = isParent;
        else
            entry.IsParentEntity = newBranchName == DefaultBranchName;

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

        PublicEntityGroup group = new();
        var entry = new PublicEntity();
        await _tx.RunAsync(async token =>
        {
            group = await FindOrCreateGroupAsync(canonical, entityType, actor.UserId, token);
            entry.Group = group;
            entry.GroupId = group.Id;
            entry.Governorate = governorate;
            entry.BranchName = branchName;
            entry.IsParentEntity = request.IsParentEntity ?? (branchName == DefaultBranchName);
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
            entry.IsParentEntity = isParent;
        else
            entry.IsParentEntity = newBranchName == DefaultBranchName;

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
        if (e.Entry != null && e.Entry.Governorate == governorate) return true;
        if (e.Group != null && e.Group.Entries.Any(en => en.Governorate == governorate)) return true;
        return false;
    }

    private async Task<List<PublicEntityChangeEvent>> GetFilteredChangeEventsAsync(EntityChangeEventQuery query, CancellationToken ct)
    {
        var all = await _entities.ListChangeEventsAsync(ct);
        var governorate = NormalizeOptional(query.Governorate);
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

    public async Task<PagedResult<EntityChangeEventDto>> ListChangeEventsAsync(EntityChangeEventQuery query, CancellationToken ct = default)
    {
        var filtered = await GetFilteredChangeEventsAsync(query, ct);
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var total = filtered.Count;
        var items = filtered.Skip((page - 1) * perPage).Take(perPage).Select(ToChangeEventDto).ToList();
        return new PagedResult<EntityChangeEventDto> { Items = items, Page = page, PerPage = perPage, TotalCount = total };
    }

    public async Task<byte[]> ExportChangeEventsAsync(EntityChangeEventQuery query, CancellationToken ct = default)
    {
        var filtered = await GetFilteredChangeEventsAsync(query, ct);
        var items = filtered.Take(5000).Select(ToChangeEventDto).ToList();
        await _audit.LogAsync("system", "export_change_events",
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
            throw new ArgumentException("نوع الجهة غير صالح (ministry/administration/authority/foundation/company)");
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
                {
                    foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == entryId))
                        a.RegistryId = targetEntryId;
                    foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == entryId))
                        e.RegistryId = targetEntryId;
                    foreach (var ea in doc.ExecutionApplicants.Where(ea => ea.RegistryId == entryId))
                        ea.RegistryId = targetEntryId;
                    doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);
                }
                affectedDocs = linkedDocs.Count;

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

                    // ترحيل روابط RegistryId
                    var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, token);
                    foreach (var doc in linkedDocs)
                    {
                        if (!affectedDocsById.ContainsKey(doc.Id))
                            affectedDocsById[doc.Id] = doc;
                    }

                    foreach (var doc in linkedDocs)
                    {
                        foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == ae.Id))
                            a.RegistryId = targetEntry.Id;
                        foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == ae.Id))
                            e.RegistryId = targetEntry.Id;
                        foreach (var ea in doc.ExecutionApplicants.Where(ea => ea.RegistryId == ae.Id))
                            ea.RegistryId = targetEntry.Id;
                        doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);
                    }

                    // إيقاف القيد المُدمَج
                    ae.IsActive = false;

                    // إضافة الأسماء البديلة
                    var fullName = $"{absorbedGroup.CanonicalName} — {ae.Governorate} / {ae.BranchName}";
                    var normalizedEntry = ArabicNameNormalizer.Normalize(absorbedGroup.CanonicalName);
                    if (!targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normalizedEntry))
                    {
                        targetEntry.Aliases.Add(new PublicEntityAlias
                        {
                            PublicEntityId = targetEntry.Id,
                            AliasText = absorbedGroup.CanonicalName,
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

                    // ترحيل الأسماء البديلة السابقة للقيد الممتصّ (المُلغى فعليًا) إلى القيد
                    // الهدف كأسماء «للبحث فقط». إذ إن إلغاء القيد الممتصّ يسلبه أسماءه البديلة
                    // الموجودة مسبقًا (التي أضافها المستخدمون سابقًا) فلا يعود البحث يجدها؛
                    // بترحيلها إلى الهدف يظل البحث بالاسم القديم البديل يجد الجهة بعد الدمج
                    // (مواءمة سلوك إعادة التسمية والوحدة 7-هـ/7-و). تُستثنى الأسماء المكررة
                    // والاسم المعياري والاسم الكامل المُعالجان أعلاه.
                    foreach (var priorAlias in ae.Aliases)
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
        var targetKeySet = new HashSet<string>(activeTarget.Select(e => $"{e.Governorate}|{e.BranchName}"), StringComparer.Ordinal);

        var warnings = new List<string>();
        var absorbedDtos = new List<AbsorbedGroupUnifyPreviewDto>();
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

            var activeAbsorbed = absorbedEntries.Where(e => e.IsActive).ToList();
            totalToMove += activeAbsorbed.Count;

            if (!string.Equals(absorbedGroup.EntityType, targetGroup.EntityType, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"تنبيه: نوع الجهة مختلف — «{absorbedGroup.CanonicalName}» ({absorbedGroup.EntityType}) و«{targetGroup.CanonicalName}» ({targetGroup.EntityType})");

            foreach (var ae in activeAbsorbed)
            {
                var key = $"{ae.Governorate}|{ae.BranchName}";
                if (targetKeySet.Contains(key))
                    warnings.Add($"تعارض: القيد «{ae.Governorate}/{ae.BranchName}» من «{absorbedGroup.CanonicalName}» موجود مسبقًا في «{targetGroup.CanonicalName}»");
            }

            var govs = activeAbsorbed.Select(e => e.Governorate).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            absorbedDtos.Add(new AbsorbedGroupUnifyPreviewDto(absorbedGroup.Id, absorbedGroup.CanonicalName, activeAbsorbed.Count, govs));
        }

        return new UnifyNamesPreviewResponse(targetGroup.CanonicalName, absorbedDtos, totalToMove, warnings);
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

            var targetKeySet = new HashSet<string>(activeTarget.Select(e => $"{e.Governorate}|{e.BranchName}"), StringComparer.Ordinal);

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
            int aliasesAdded = 0;
            int totalAffectedDocs = 0;
            var oldNames = new List<string>();
            var movedEntryIds = new List<int>();
            var affectedDocsById = new Dictionary<int, Document>();
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

                foreach (var ae in activeAbsorbed)
                {
                    var key = $"{ae.Governorate}|{ae.BranchName}";
                    if (targetKeySet.Contains(key))
                        throw new ArgumentException($"تعارض: القيد «{ae.Governorate}/{ae.BranchName}» من «{absorbedGroup.CanonicalName}» موجود مسبقًا في «{targetGroup.CanonicalName}»");
                }

                oldNames.Add(absorbedGroup.CanonicalName);

                foreach (var ae in activeAbsorbed)
                {
                    // 1) نقل القيد إلى مجموعة الهدف
                    ae.GroupId = targetGroup.Id;
                    targetKeySet.Add($"{ae.Governorate}|{ae.BranchName}");
                    movedEntryIds.Add(ae.Id);
                    entriesMoved++;

                    // 3) حفظ الاسم الممتصّ اسمًا بديلًا «للبحث فقط» على القيد المنقول
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

            // 6) ترحيل مندوبي الجهات الممتصة إلى الهدف (على مستوى المجموعة)
            var absorbedIdsSet = new HashSet<int>(absorbedIdsDistinct);
            await MigrateDelegatesAsync(absorbedIdsSet, targetGroup.Id, null, null, token);

            // 7) سجل التغيير
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                targetGroupId = targetGroup.Id,
                targetGroup = targetGroup.CanonicalName,
                absorbedGroupIds = absorbedIdsDistinct,
                oldCanonicalNames = oldNames,
                entriesMoved,
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
                details: $"توحيد تسمية {groupsUnified} هويات في «{targetGroup.CanonicalName}» — {entriesMoved} قيدًا نُقل، {totalAffectedDocs} ملفًا متأثرًا",
                ct: token);

            // تنبيه عام لكل المحامين + تنبيه خاص لرؤساء الأقسام
            await BroadcastEntityChangeToAllLawyersAsync(
                EntityChangeMessages.UnifyLawyersAlert(absorbedNamesJoined, targetGroup.CanonicalName, decreeKind ?? "", decreeNumber ?? "", decreeDate),
                actor.UserId, token);
            await BroadcastToAllHeadsAsync(
                EntityChangeMessages.UnifyHeadsAlert(absorbedNamesJoined, targetGroup.CanonicalName, decreeKind ?? "", decreeNumber ?? "", decreeDate),
                actor.UserId, token);

            return new UnifyNamesResponse(targetGroup.Id, targetGroup.CanonicalName, groupsUnified, entriesMoved, changeEvent.Id);
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
    /// القيد المطابق لفرعه عبر <paramref name="entryTargetByAbsorbedEntry"/> (عند الدمج حيث
    /// تُطوى القيود فرعًا بفرع)، ويسقط على <paramref name="defaultTargetEntryId"/> عند غياب
    /// المطابق؛ وفي مسار التوحيد (لا طيّ فرعي — القيود انتقلت كاملة) يُترك قيده كما هو مع
    /// تمرير قيمتي الفارق null.
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
