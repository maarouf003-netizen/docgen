using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>
/// إنشاء قيد نهائي في سجل الجهات العامة — الإدارة/الرئيس بنطاقه، مع اعتماد المراجعة
/// للمحامي (`NeedsReview`)، وتدقيق داخل معاملة واحدة.
/// </summary>
public sealed record CreatePublicEntityCommand(
    CreatePublicEntityRequest Request,
    EntityRegistryActor Actor) : ICommand<PublicEntityEntryDto>;

internal sealed class CreatePublicEntityCommandHandler
    : CommandHandlerBase<CreatePublicEntityCommand, PublicEntityEntryDto>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public CreatePublicEntityCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<CreatePublicEntityCommand, PublicEntityEntryDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<PublicEntityEntryDto> HandleCoreAsync(
        CreatePublicEntityCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        var canonical = _rules.Required(request.CanonicalName, "اسم الجهة مطلوب", 200);
        var entityType = _rules.ValidEntityType(request.EntityType);
        var governorate = _rules.Required(request.Governorate, "المحافظة مطلوبة", 100);
        var branchName = _rules.RequiredWithFallback(request.BranchName, PublicEntityBranchCatalog.ParentBranchName, 200);
        var citationFormula = _rules.ValidCitationFormula(request.CitationFormula, CitationFormulaCatalog.AddToJob);
        var aliases = _rules.CleanAliases(request.Aliases, ArabicNameNormalizer.Normalize(canonical));
        var coverageLabel = _rules.ValidateCoverageLabel(request.CoverageLabel);

        await _rules.EnsureHeadScopeAsync(actor, null, governorate, ct);
        await _rules.EnsureNoDuplicateEntryAsync(excludeEntryId: null, canonical, governorate, branchName, ct);

        // حوكمة S1 — منع صريح للجميع: الرئيس (قسم/شعبة) والمحامي لا ينشئان قيدًا «لجهة أم»
        // صراحةً (المسار المركزي للإدارة)، والاشتقاق الضمني من اسم الفرع الافتراضي
        // مُجمَّد لهما — الإنشاء بالاسم الافتراضي يبقى فرعًا عاديًا (مع NeedsReview للمحامي).
        if (request.IsParentEntity == true && actor.Role is UserRole.Head or UserRole.SubHead or UserRole.Lawyer)
            throw new UnauthorizedAccessException("لا يُنشئ قيد «الجهة الأم» إلا الإدارة عبر المسارات المركزية");
        var isParentEntity = request.IsParentEntity
            ?? (branchName == PublicEntityBranchCatalog.ParentBranchName && actor.Role is not (UserRole.Head or UserRole.SubHead or UserRole.Lawyer));

        PublicEntityGroup group = new();
        var entry = new PublicEntity();
        await _tx.RunAsync(async token =>
        {
            group = await _rules.FindOrCreateGroupAsync(canonical, entityType, actor.UserId, token);
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

        return _rules.ToEntryDto(group, entry);
    }
}

/// <summary>
/// تعديل قيد جهة / إعادة تسمية جماعية — الإدارة/الرئيس بنطاقه، مع مزامنة النصوص
/// وأحداث التغيير والتدقيق في معاملة واحدة.
/// </summary>
public sealed record UpdatePublicEntityCommand(
    int EntryId,
    UpdatePublicEntityRequest Request,
    EntityRegistryActor Actor) : ICommand<PublicEntityEntryDto?>;

internal sealed class UpdatePublicEntityCommandHandler
    : CommandHandlerBase<UpdatePublicEntityCommand, PublicEntityEntryDto?>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public UpdatePublicEntityCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<UpdatePublicEntityCommand, PublicEntityEntryDto?>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<PublicEntityEntryDto?> HandleCoreAsync(
        UpdatePublicEntityCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        var entry = await _entities.GetEntryWithDetailsAsync(command.EntryId, ct);
        if (entry is null)
            return null;
        var group = entry.Group;

        OptimisticConcurrencyGuard.EnsureCurrent(entry, request.Version, "قيد الجهة");

        string? newCanonical = null;
        if (!string.IsNullOrWhiteSpace(request.CanonicalName)
            && !string.Equals(request.CanonicalName.Trim(), group.CanonicalName, StringComparison.Ordinal))
        {
            newCanonical = _rules.Required(request.CanonicalName, "اسم الجهة مطلوب", 200);
            await _rules.EnsureCanonicalAvailableAsync(newCanonical, group.Id, ct);
        }

        var newGovernorate = entry.Governorate;
        if (!string.IsNullOrWhiteSpace(request.Governorate))
            newGovernorate = _rules.Required(request.Governorate, "المحافظة مطلوبة", 100);

        var newBranchName = entry.BranchName;
        if (!string.IsNullOrWhiteSpace(request.BranchName))
            newBranchName = _rules.RequiredWithFallback(request.BranchName, PublicEntityBranchCatalog.ParentBranchName, 200);

        // نطاق الرئيس (قسم/شعبة): قيود محافظته فقط، ولا يعيد تسمية هوية تشمل محافظات أخرى (د5/د6).
        await _rules.EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
        // حارس الجهة الأم (C3/F3): لا يحرّر الرئيس قيد «الجهة الأم» إطلاقًا — الاقتراح فقط.
        _rules.GuardHeadCannotEditParent(actor, entry);
        if (actor.Role is UserRole.Head or UserRole.SubHead)
        {
            if (!string.Equals(newGovernorate, entry.Governorate, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("الرئيس مقصور على قيود محافظة فرعه");
            if (newCanonical is not null
                && group.Entries.Any(e => e.Id != entry.Id && e.Governorate != entry.Governorate))
                throw new UnauthorizedAccessException("إعادة تسمية الهوية تشمل قيودًا خارج محافظة فرعك");
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            group.EntityType = _rules.ValidEntityType(request.EntityType);
        if (!string.IsNullOrEmpty(request.Status))
        {
            if (!EntityStatusCatalog.IsValid(request.Status))
                throw new ArgumentException("حالة القيد غير صالحة (final/pending)");
            entry.Status = request.Status!;
        }
        if (request.IsActive.HasValue)
            entry.IsActive = request.IsActive.Value;
        if (!string.IsNullOrWhiteSpace(request.CitationFormula))
            entry.CitationFormula = _rules.ValidCitationFormula(request.CitationFormula, entry.CitationFormula);
        if (request.CoverageLabel is not null)
            entry.CoverageLabel = _rules.ValidateCoverageLabel(request.CoverageLabel);
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
            entry.IsParentEntity = newBranchName == PublicEntityBranchCatalog.ParentBranchName && actor.Role is not (UserRole.Head or UserRole.SubHead);
        }

        await _rules.EnsureNoDuplicateEntryAsync(entry.Id, group.CanonicalName, newGovernorate, newBranchName, ct);

        // حقول المرسوم للتعديلات العامة بمرسوم (المدير/المشرف) — تاريخ حر نصه مثال 1/8/2026
        var decreeKind = _rules.NormalizeOptional(request.DecreeKind);
        var decreeNumber = _rules.NormalizeOptional(request.DecreeNumber);
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
        _rules.SetGroupNorm(group);
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
                ? await _rules.SyncTextsAfterRenameAsync(oldCanonical, newCanonical!, actor.Name, token)
                : new List<Document>();
            var affected = affectedDocs.Count;

            if (renamed && wasNeedsReview && createdByLawyer)
                await _rules.InsertRenameNoticeToCreatorAsync(entry, oldCanonical, group.CanonicalName, token);

            // مزامنة لقطات الاستئنافات عند إعادة تسمية قيد معيّن
            if (affectedDocs.Count > 0)
                await _rules.SyncAppealsAfterEntityChangeAsync(affectedDocs, actor, token);

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
                await _rules.TrackChangeEventAsync(changeEvent, token);
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

        return _rules.ToEntryDto(group, entry);
    }
}

/// <summary>إضافة اسم بديل لقيد جهة — الرئيس بنطاقه/الإدارة، بتدقيق داخل معاملة واحدة.</summary>
public sealed record AddPublicEntityAliasCommand(
    int EntryId,
    AddPublicEntityAliasRequest Request,
    EntityRegistryActor Actor) : ICommand<PublicEntityEntryDto?>;

internal sealed class AddPublicEntityAliasCommandHandler
    : CommandHandlerBase<AddPublicEntityAliasCommand, PublicEntityEntryDto?>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public AddPublicEntityAliasCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<AddPublicEntityAliasCommand, PublicEntityEntryDto?>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<PublicEntityEntryDto?> HandleCoreAsync(
        AddPublicEntityAliasCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        var entry = await _entities.GetEntryWithDetailsAsync(command.EntryId, ct);
        if (entry is null)
            return null;

        await _rules.EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);

        var aliasText = _rules.Required(request.AliasText, "الاسم البديل مطلوب", 500);
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

        return _rules.ToEntryDto(entry.Group, entry);
    }
}

/// <summary>اعتماد قيد كما هو: يقفل المراجعة دون تعديل ودون إشعار للمُدخِل (حسب القرار).</summary>
public sealed record ApprovePublicEntityReviewCommand(
    int EntryId,
    EntityRegistryActor Actor) : ICommand<PublicEntityEntryDto?>;

internal sealed class ApprovePublicEntityReviewCommandHandler
    : CommandHandlerBase<ApprovePublicEntityReviewCommand, PublicEntityEntryDto?>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public ApprovePublicEntityReviewCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<ApprovePublicEntityReviewCommand, PublicEntityEntryDto?>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<PublicEntityEntryDto?> HandleCoreAsync(
        ApprovePublicEntityReviewCommand command, CancellationToken ct)
    {
        var actor = command.Actor;
        var entry = await _entities.GetEntryWithDetailsAsync(command.EntryId, ct);
        if (entry is null)
            return null;

        await _rules.EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);

        await _tx.RunAsync(async token =>
        {
            entry.NeedsReview = false;
            entry.ReviewedAtUtc = DateTime.UtcNow;
            entry.ReviewedById = actor.UserId;
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "approve_entity_review",
                details: $"اعتمد مراجعة قيد الجهة: {entry.Group.CanonicalName} ({entry.Governorate} / {entry.BranchName})", ct: token);
        }, ct);

        return _rules.ToEntryDto(entry.Group, entry);
    }
}
