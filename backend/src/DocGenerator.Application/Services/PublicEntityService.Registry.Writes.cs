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

            await _audit.LogAsync(actor.Name, "create_public_entity",
                details: $"أضاف قيد جهة: {canonical} ({governorate} / {branchName})"
                    + (entry.NeedsReview ? " — بانتظار مراجعة رئيس القسم" : string.Empty),
                ct: token);
        }, ct);

        return ToEntryDto(group, entry);
    }
    // ── تعديل قيد / إعادة تسمية جماعية (د5) ──

    public async Task<PublicEntityEntryDto?> UpdateAsync(int entryId, UpdatePublicEntityRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var entry = await _entities.GetEntryWithDetailsAsync(entryId, ct);
        if (entry is null)
            return null;
        var group = entry.Group;

        OptimisticConcurrencyGuard.EnsureCurrent(entry, request.Version, "قيد الجهة");

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
}
