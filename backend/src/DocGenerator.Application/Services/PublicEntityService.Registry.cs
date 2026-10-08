using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
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

        // نطاق الرئيس (قسم/شعبة): محافظة فرعه فقط — بلا فرع/محافظة لا يُعرض شيء
        string? headGovernorate = null;
        bool isHead = actor.Role is UserRole.Head or UserRole.SubHead;
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

        // نطاق الرئيس (قسم/شعبة): محافظته فقط — مع ضمان ظهور قيد «الجهة الأم» دائمًا (F4).
        if (actor.Role is UserRole.Head or UserRole.SubHead && actor.BranchId.HasValue)
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
        SetGroupNorm(group);
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
            await TrackChangeEventAsync(changeEvent, token);
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

        // حوكمة S1 — منع صريح للجميع: الرئيس (قسم/شعبة) والمحامي لا ينشئان قيدًا «لجهة أم»
        // صراحةً (المسار المركزي للإدارة)، والاشتقاق الضمني من اسم الفرع الافتراضي
        // مُجمَّد لهما — الإنشاء بالاسم الافتراضي يبقى فرعًا عاديًا (مع NeedsReview للمحامي).
        if (request.IsParentEntity == true && actor.Role is UserRole.Head or UserRole.SubHead or UserRole.Lawyer)
            throw new UnauthorizedAccessException("لا يُنشئ قيد «الجهة الأم» إلا الإدارة عبر المسارات المركزية");
        var isParentEntity = request.IsParentEntity
            ?? (branchName == DefaultBranchName && actor.Role is not (UserRole.Head or UserRole.SubHead or UserRole.Lawyer));

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

        // نطاق الرئيس (قسم/شعبة): قيود محافظته فقط، ولا يعيد تسمية هوية تشمل محافظات أخرى (د5/د6).
        await EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
        // حارس الجهة الأم (C3/F3): لا يحرّر الرئيس قيد «الجهة الأم» إطلاقًا — الاقتراح فقط.
        GuardHeadCannotEditParent(actor, entry);
        if (actor.Role is UserRole.Head or UserRole.SubHead)
        {
            if (!string.Equals(newGovernorate, entry.Governorate, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("الرئيس مقصور على قيود محافظة فرعه");
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
            if (isParent && actor.Role is UserRole.Head or UserRole.SubHead)
                throw new UnauthorizedAccessException("الرئيس لا يرقّي قيدًا إلى جهة أم — أرسل اقتراح تعديل للإدارة");
            entry.IsParentEntity = isParent;
        }
        else
        {
            // الاشتقاق الضمني من اسم الفرع الافتراضي مُجمَّد للرئيس (حوكمة S1)؛
            // يُحفَظ وضعه الحالي (فرع عادي) بلا رفض.
            entry.IsParentEntity = newBranchName == DefaultBranchName && actor.Role is not (UserRole.Head or UserRole.SubHead);
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
        SetGroupNorm(group);
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
                await TrackChangeEventAsync(changeEvent, token);
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
    /// قائمة «بحاجة مراجعة»: الرئيس (قسم/شعبة) يرى ما أدخله محامو فرعه (بغض النظر عن محافظة
    /// الجهة نفسها — قد يُقيم محامٍ ملفًا تنفيذيًا على جهة تتبع محافظة أخرى)، والمدير/
    /// المشرف يرىان كل السجل. رئيس بلا فرع مضبوط تعني قائمة فارغة.
    /// </summary>
    public async Task<List<PublicEntityEntryDto>> ListNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default)
    {
        int? headBranchId = null;
        if (actor.Role is UserRole.Head or UserRole.SubHead)
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
    public async Task<int> CountNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default)
    {
        int? headBranchId = null;
        if (actor.Role is UserRole.Head or UserRole.SubHead)
        {
            var branch = actor.BranchId is null ? null : await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            headBranchId = branch?.Id;
            if (headBranchId is null)
                return 0;
        }

        return await _entities.CountNeedsReviewAsync(headBranchId, ct);
    }
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
    private async Task<PublicEntityGroup> FindOrCreateGroupAsync(string canonical, string entityType, int actorUserId, CancellationToken token)
    {
        var norm = ArabicNameNormalizer.Normalize(canonical);
        var existing = await FindGroupByNormAsync(norm, token);
        if (existing is not null)
            return existing;
        var created = new PublicEntityGroup
        {
            CanonicalName = canonical,
            EntityType = entityType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        SetGroupNorm(created);
        return created;
    }
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
}
