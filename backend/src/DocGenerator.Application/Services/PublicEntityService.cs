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
    bool IncludeInactive = true);

public interface IPublicEntityService
{
    Task<PagedResult<PublicEntityEntryDto>> ListAsync(EntityRegistryListQuery query, CancellationToken ct = default);

    Task<PublicEntityEntryDto> CreateAsync(CreatePublicEntityRequest request, EntityRegistryActor actor, CancellationToken ct = default);
    Task<PublicEntityEntryDto?> UpdateAsync(int entryId, UpdatePublicEntityRequest request, EntityRegistryActor actor, CancellationToken ct = default);
    Task<PublicEntityEntryDto?> AddAliasAsync(int entryId, AddPublicEntityAliasRequest request, EntityRegistryActor actor, CancellationToken ct = default);

    /// <summary>قيود بانتظار مراجعة رئيس القسم ضمن نطاقه (المدير/المشرف يرىان الكل).</summary>
    Task<List<PublicEntityEntryDto>> ListNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default);

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
}

/// <summary>
/// خدمة السجل المرجعي للجهات العامة (نموذج الحوكمة الجديد): أي جهة يُدخلها
/// محامٍ تُعتمد أصوليًا نهائية فورًا لكنها تبقى «بحاجة مراجعة» مع تنبيه رؤساء
/// محافظتها؛ الاعتماد يقفل المراجعة بصمت، والتعديل — وتغيير التسمية تحديدًا —
/// يبلّغ المُدخِل بالاسم القديم والجديد. الإدارة تعدّل كل السجل بتنفيذ فوري.
/// إعادة التسمية الجماعية تزامن الأعمدة النصية ضمن معاملة واحدة (د5)، وأداة
/// الاستيراد التاريخي تعتمد نهائيًا مباشرة (د12).
/// </summary>
public sealed class PublicEntityService : IPublicEntityService
{
    private const string DefaultBranchName = "الفرع الرئيسي";

    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<HeadAlert> _headAlerts;
    private readonly IRepository<PublicEntityChangeEvent> _changeEvents;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public PublicEntityService(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IRepository<HeadAlert> headAlerts,
        IRepository<PublicEntityChangeEvent> changeEvents,
        IRepository<DocumentOccurrence> occurrences,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _entities = entities;
        _branches = branches;
        _headAlerts = headAlerts;
        _changeEvents = changeEvents;
        _occurrences = occurrences;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    // ── القراءة ──

    public async Task<PagedResult<PublicEntityEntryDto>> ListAsync(EntityRegistryListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var qNorm = ArabicNameNormalizer.Normalize(query.Q);
        var governorate = NormalizeOptional(query.Governorate);
        var status = NormalizeOptional(query.Status);

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        var entries = groups
            .SelectMany(g => g.Entries.Select(e => (Group: g, Entry: e)))
            .Where(x => query.IncludePending || x.Entry.Status != EntityStatusCatalog.Pending)
            .Where(x => query.IncludeInactive || x.Entry.IsActive)
            .Where(x => governorate is null || x.Entry.Governorate == governorate)
            .Where(x => status is null || x.Entry.Status == status)
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

        await EnsureHeadScopeAsync(actor, governorate, ct);
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
            entry.CitationFormula = citationFormula;
            entry.CoverageLabel = coverageLabel;
            entry.Status = EntityStatusCatalog.Final;
            entry.CreatedById = actor.UserId;
            entry.CreatedAt = DateTime.UtcNow;
            entry.IsActive = true;
            // نموذج الحوكمة الجديد: ما أدخله محامٍ يُعتمد فورًا ويبقى بانتظار
            // مراجعة رئيس قسم المحافظة، أما الإدارة/الرئيس فيدخلون مُراجَعًا.
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
    /// تنبيه رؤساء الأقسام النشطين لمحافظة القيد المُدخل حديثًا بواسطة محامٍ:
    /// «المحامي فلان أدخل جهة عامة جديدة يرجى مراجعتها». إن لم يوجد رئيس بمحافظة
    /// مطابقة فلا تنبيه — صفحة المراجعة تعرضه للمدير/المشرف على كل الأحوال.
    /// </summary>
    private async Task InsertEntryReviewAlertsAsync(PublicEntity entry, string? actorName, CancellationToken token)
    {
        var creator = await _entities.GetEntryWithDetailsAsync(entry.Id, token);
        var creatorFullName = creator?.CreatedBy?.FullName ?? actorName ?? "محامٍ";
        var heads = await _entities.ListActiveHeadsByGovernorateAsync(entry.Governorate, token);
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
        await EnsureHeadScopeAsync(actor, entry.Governorate, ct);
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

        await EnsureNoDuplicateEntryAsync(entry.Id, group.CanonicalName, newGovernorate, newBranchName, ct);

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
            var affected = renamed
                ? await SyncTextsAfterRenameAsync(oldCanonical, newCanonical!, actor.Name, token)
                : 0;

            if (renamed && wasNeedsReview && createdByLawyer)
                await InsertRenameNoticeToCreatorAsync(entry, oldCanonical, group.CanonicalName, token);

            await _uow.SaveChangesAsync(token);
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

        await EnsureHeadScopeAsync(actor, entry.Governorate, ct);

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
    /// قائمة «بحاجة مراجعة»: رئيس القسم يرى محافظة فرعه حصرًا، والمدير/المشرف
    /// يرىان كل السجل. الفرع بلا محافظة مضبوطة يعني قائمة فارغة للرئيس.
    /// </summary>
    public async Task<List<PublicEntityEntryDto>> ListNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default)
    {
        string? governorateFilter = null;
        if (actor.Role == UserRole.Head)
        {
            var branch = actor.BranchId is null ? null : await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            governorateFilter = NormalizeOptional(branch?.Governorate);
            if (governorateFilter is null)
                return new List<PublicEntityEntryDto>();
        }

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        return groups
            .SelectMany(g => g.Entries.Select(e => (Group: g, Entry: e)))
            .Where(x => x.Entry.NeedsReview)
            .Where(x => governorateFilter is null || x.Entry.Governorate == governorateFilter)
            .OrderByDescending(x => x.Entry.CreatedAt)
            .Select(x => ToEntryDto(x.Group, x.Entry))
            .ToList();
    }

    /// <summary>اعتماد قيد كما هو: يقفل المراجعة دون تعديل ودون إشعار للمُدخِل (حسب القرار).</summary>
    public async Task<PublicEntityEntryDto?> ApproveReviewAsync(int entryId, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var entry = await _entities.GetEntryWithDetailsAsync(entryId, ct);
        if (entry is null)
            return null;

        await EnsureHeadScopeAsync(actor, entry.Governorate, ct);

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
    /// في سجل تعديلات الحقول. تعمل داخل معاملة المتصل وتعيد عدد الملفات المتأثرة.
    /// </summary>
    private async Task<int> SyncTextsAfterRenameAsync(string oldCanonical, string newCanonical, string? actorName, CancellationToken token)
    {
        var oldNorm = ArabicNameNormalizer.Normalize(oldCanonical);
        var newNorm = ArabicNameNormalizer.Normalize(newCanonical);
        if (oldNorm.Length == 0 || oldNorm == newNorm)
            return 0;

        var logs = new Dictionary<int, List<DocumentFieldChange>>();
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
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
        }

        await _uow.SaveChangesAsync(token);
        var details = $"مزامنة إعادة تسمية الجهة: «{oldCanonical}» ← «{newCanonical}»";
        foreach (var (documentId, changes) in logs)
            await _audit.LogDocumentChangeAsync(actorName, "rename_public_entity_sync",
                documentId, documentType: null, details, changes, token);
        return logs.Count;
    }

    // ── مساعدات خاصة ──

    private async Task EnsureHeadScopeAsync(EntityRegistryActor actor, string targetGovernorate, CancellationToken ct)
    {
        if (actor.Role != UserRole.Head)
            return;
        var branch = actor.BranchId is null ? null : await _branches.GetByIdAsync(actor.BranchId.Value, ct);
        var branchGov = NormalizeOptional(branch?.Governorate);
        if (branchGov is null || !string.Equals(branchGov, targetGovernorate.Trim(), StringComparison.Ordinal))
            throw new UnauthorizedAccessException("رئيس القسم مقصور على قيود محافظة فرعه؛ اطلب من الإدارة ضبط محافظة الفرع أولًا");
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

    /// <summary>قيمة اختيارية ببديل افتراضي معتمد («الفرع الرئيسي») مع سقف الطول.</summary>
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
                await EnsureHeadScopeAsync(actor, targetEntry.Governorate, token);
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
                    doc.ApplicantRegistryId = toGroupId;
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

                await EnsureHeadScopeAsync(actor, entry.Governorate, token);

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
                    doc.ApplicantRegistryId = toGroupId;
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
                await EnsureHeadScopeAsync(actor, entry.Governorate, token);

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
                    doc.ApplicantRegistryId = request.TargetGroupId;

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

            var absorbedGroupsProcessed = 0;
            var entriesMigrated = 0;
            var aliasesAdded = 0;
            var totalAffectedDocs = 0;
            var affectedGovernorates = new HashSet<string>();
            var affectedDocsById = new Dictionary<int, Document>();
            var branchMap = new List<object>();

            foreach (var absorbedId in request.AbsorbedGroupIds.Distinct())
            {
                var absorbedGroup = await _entities.GetGroupAsync(absorbedId, token)
                    ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
                if (!absorbedGroup.IsActive)
                    throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");

                var absorbedEntries = await _entities.ListEntriesByGroupAsync(absorbedId, token);

                if (absorbedEntries.Any(e => e.NeedsReview))
                    throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{absorbedGroup.CanonicalName}» قبل الدمج");

                foreach (var ae in absorbedEntries.Where(e => e.IsActive))
                {
                    var matchedSurvivor = activeSurvivorEntries
                        .FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);

                    var targetEntry = matchedSurvivor ?? activeSurvivorEntries.First();

                    // ترحيل روابط RegistryId
                    var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, token);
                    foreach (var doc in linkedDocs)
                    {
                        if (!affectedDocsById.ContainsKey(doc.Id))
                            affectedDocsById[doc.Id] = doc;
                    }
                    affectedGovernorates.Add(ae.Governorate);

                    foreach (var doc in linkedDocs)
                    {
                        foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == ae.Id))
                            a.RegistryId = targetEntry.Id;
                        foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == ae.Id))
                            e.RegistryId = targetEntry.Id;
                        doc.ApplicantRegistryId = targetEntry.GroupId;
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

            totalAffectedDocs = affectedDocsById.Count;

            // مزامنة النصوص للملفات المتأثرة
            var affectedDocs = affectedDocsById.Values.ToList();
            if (affectedDocs.Count > 0)
            {
                await SyncTextsAfterFoldAsync(affectedDocs, actor.Name, token);
            }

            // حدث الدمج الأب
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                survivorGroupId = survivorGroup.Id,
                survivorGroup = survivorGroup.CanonicalName,
                absorbedGroupIds = request.AbsorbedGroupIds,
                entriesMigrated,
                aliasesAdded,
                totalAffectedDocs,
                branchMap,
                unifyTexts = request.UnifyTexts,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = survivorGroup.Id,
                ActionKind = ActionKindCatalog.Merge,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _changeEvents.AddAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);

            // وقوعات آلية لكل ملف متأثر
            foreach (var docId in affectedDocsById.Keys)
            {
                var occurrence = new DocumentOccurrence
                {
                    DocumentId = docId,
                    OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                    EventDate = DateTime.UtcNow,
                    CreatedById = actor.UserId,
                    Details = $"تم دمج جهات في «{survivorGroup.CanonicalName}»",
                };
                await _occurrences.AddAsync(occurrence, token);
            }

            // تنبيه رؤساء الأقسام المتأثرين
            foreach (var gov in affectedGovernorates)
            {
                var heads = await _entities.ListActiveHeadsByGovernorateAsync(gov, token);
                foreach (var head in heads.Where(h => h.BranchId.HasValue))
                {
                    var msg = $"دمج جهات في «{survivorGroup.CanonicalName}» — يرجى أخذ العلم";
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

            await _audit.LogAsync(actor.Name, "merge_entity_registry",
                documentId: null, documentType: null,
                details: $"دمج {absorbedGroupsProcessed} هويات أم في «{survivorGroup.CanonicalName}» — {entriesMigrated} قيد، {totalAffectedDocs} ملفًا متأثرًا",
                ct: token);

            return new MergeCommitResponse(absorbedGroupsProcessed, entriesMigrated, aliasesAdded, totalAffectedDocs, changeEvent.Id);
        }, ct);
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
            doc.SearchText = DocumentSearchTextBuilder.Build(doc);
            doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
        }
        await _uow.SaveChangesAsync(token);
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
        entry.CoverageLabel);
}
