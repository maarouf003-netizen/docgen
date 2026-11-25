using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// قواعد كتابة السجل المرجعي للجهات العامة (نموذج الحوكمة الجديد): الحرّاس والتحقق
/// ومزامنة النصوص وأحداث التغيير والتنبيهات المشتركة بين معالجي الأوامر — مصدر واحد
/// يستهلكه المعالج عبر <see cref="IPublicEntityWriteRules"/>؛ القراءات صارت استعلامات
/// مستقلة (`P4-01cg`/`P4-01cj`) لا تمرّ بهذه الخدمة.
/// </summary>
public sealed partial class PublicEntityService : IPublicEntityWriteRules
{
    /// <summary>الاسم الافتراضي لفرع قيد الجهة الأم — من الكتالوج لا من حرفي مكرر.</summary>
    private const string DefaultBranchName = PublicEntityBranchCatalog.ParentBranchName;

    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;
    private readonly IHeadAlertRepository _headAlerts;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IRepository<PublicEntityChangeEvent> _changeEvents;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IUserRepository _users;
    private readonly IAppealRepository _appeals;

    public PublicEntityService(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IHeadAlertRepository headAlerts,
        IHeadAlertOutbox alertOutbox,
        IRepository<PublicEntityChangeEvent> changeEvents,
        IRepository<DocumentOccurrence> occurrences,
        IUnitOfWork uow,
        IAuditLogger audit,
        IUserRepository users,
        IAppealRepository appeals)
    {
        _entities = entities;
        _branches = branches;
        _headAlerts = headAlerts;
        _alertOutbox = alertOutbox;
        _changeEvents = changeEvents;
        _occurrences = occurrences;
        _uow = uow;
        _audit = audit;
        _users = users;
        _appeals = appeals;
    }

    // ── تنفيذ عقد مساعدات الكتابة المشتركة (P4-01be) ──────────────────────

    Task IPublicEntityWriteRules.EnsureHeadScopeAsync(
        EntityRegistryActor actor, PublicEntity? entry, string? fallbackGovernorate, CancellationToken ct)
        => EnsureHeadScopeAsync(actor, entry, fallbackGovernorate, ct);

    Task IPublicEntityWriteRules.EnsureCanonicalAvailableAsync(string canonical, int excludeGroupId, CancellationToken ct)
        => EnsureCanonicalAvailableAsync(canonical, excludeGroupId, ct);

    Task IPublicEntityWriteRules.EnsureNoDuplicateEntryAsync(
        int? excludeEntryId, string canonical, string governorate, string branchName, CancellationToken ct)
        => EnsureNoDuplicateEntryAsync(excludeEntryId, canonical, governorate, branchName, ct);

    Task<PublicEntityGroup> IPublicEntityWriteRules.FindOrCreateGroupAsync(
        string canonical, string entityType, int actorUserId, CancellationToken token)
        => FindOrCreateGroupAsync(canonical, entityType, actorUserId, token);

    Task IPublicEntityWriteRules.TrackChangeEventAsync(PublicEntityChangeEvent changeEvent, CancellationToken token)
        => TrackChangeEventAsync(changeEvent, token);

    Task<List<Document>> IPublicEntityWriteRules.SyncTextsAfterRenameAsync(
        string oldCanonical, string newCanonical, string? actorName, CancellationToken token)
        => SyncTextsAfterRenameAsync(oldCanonical, newCanonical, actorName, token);

    Task IPublicEntityWriteRules.SyncTextsAfterFoldAsync(
        List<Document> linkedDocs, string? actorName, CancellationToken token)
        => SyncTextsAfterFoldAsync(linkedDocs, actorName, token);

    Task IPublicEntityWriteRules.SyncAppealsAfterEntityChangeAsync(
        IReadOnlyCollection<Document> affectedDocs, EntityRegistryActor actor, CancellationToken token)
        => SyncAppealsAfterEntityChangeAsync(affectedDocs, actor, token);

    Task IPublicEntityWriteRules.InsertRenameNoticeToCreatorAsync(
        PublicEntity entry, string oldName, string newName, CancellationToken token)
        => InsertRenameNoticeToCreatorAsync(entry, oldName, newName, token);

    PublicEntityEntryDto IPublicEntityWriteRules.ToEntryDto(PublicEntityGroup group, PublicEntity entry)
        => ToEntryDto(group, entry);

    string IPublicEntityWriteRules.Required(string? value, string emptyMessage, int maxLength)
        => Required(value, emptyMessage, maxLength);

    string IPublicEntityWriteRules.RequiredWithFallback(string? value, string fallback, int maxLength)
        => RequiredWithFallback(value, fallback, maxLength);

    string IPublicEntityWriteRules.ValidEntityType(string? value)
        => ValidEntityType(value);

    string IPublicEntityWriteRules.ValidCitationFormula(string? value, string fallback)
        => ValidCitationFormula(value, fallback);

    List<string> IPublicEntityWriteRules.CleanAliases(IEnumerable<string>? aliases, string canonicalNorm)
        => CleanAliases(aliases, canonicalNorm);

    string? IPublicEntityWriteRules.ValidateCoverageLabel(string? value)
        => ValidateCoverageLabel(value);

    string? IPublicEntityWriteRules.NormalizeOptional(string? value)
        => NormalizeOptional(value);

    void IPublicEntityWriteRules.SetGroupNorm(PublicEntityGroup group)
        => SetGroupNorm(group);

    void IPublicEntityWriteRules.GuardHeadCannotEditParent(EntityRegistryActor actor, PublicEntity entry)
        => GuardHeadCannotEditParent(actor, entry);

    void IPublicEntityWriteRules.GuardNotParentEntry(PublicEntity entry, string? message)
        => GuardNotParentEntry(entry, message);

    Task<List<ImportPreviewItemDto>> IPublicEntityWriteRules.BuildImportCandidatesAsync(CancellationToken ct)
        => BuildImportCandidatesAsync(ct);

    Task<PublicEntityGroup?> IPublicEntityWriteRules.FindGroupByNormAsync(string norm, CancellationToken token)
        => FindGroupByNormAsync(norm, token);

    ParentEditSuggestionDto IPublicEntityWriteRules.ToParentEditSuggestionDto(ParentEditSuggestion suggestion)
        => ToParentEditSuggestionDto(suggestion);

    Task<PublicEntity> IPublicEntityWriteRules.GetActiveGroupEntryAsync(
        int groupId, int entryId, EntityRegistryActor actor, CancellationToken ct)
        => GetActiveGroupEntryAsync(groupId, entryId, actor, ct);

    Task<List<Document>> IPublicEntityWriteRules.SyncBranchLabelsAsync(
        PublicEntity entry, string newBranch, CancellationToken ct)
        => SyncBranchLabelsAsync(entry, newBranch, ct);

    void IPublicEntityWriteRules.AddEachExtraAlias(PublicEntity entry, string text, ref int aliasesAdded)
        => AddEachExtraAlias(entry, text, ref aliasesAdded);

    string IPublicEntityWriteRules.FullEntryName(string canonical, string governorate, string branchName)
        => FullEntryName(canonical, governorate, branchName);

    Task IPublicEntityWriteRules.InsertBranchOccurrencesAsync(
        List<Document> docs, string message, EntityRegistryActor actor, CancellationToken ct)
        => InsertBranchOccurrencesAsync(docs, message, actor, ct);

    Task IPublicEntityWriteRules.InsertBranchChangeAlertAsync(
        PublicEntity entry, string message, string actionKind, EntityRegistryActor actor, CancellationToken ct)
        => InsertBranchChangeAlertAsync(entry, message, actionKind, actor, ct);

    void IPublicEntityWriteRules.RepointEntryLinks(Document document, int sourceEntryId, int targetEntryId)
        => RepointEntryLinks(document, sourceEntryId, targetEntryId);

    void IPublicEntityWriteRules.AddFoldAliases(
        PublicEntity target, string canonical, PublicEntity absorbed, ref int aliasesAdded)
        => AddFoldAliases(target, canonical, absorbed, ref aliasesAdded);

    Task<int> IPublicEntityWriteRules.MigrateDelegatesAsync(
        HashSet<int> absorbedIds,
        int targetGroupId,
        int? defaultTargetEntryId,
        IReadOnlyDictionary<int, int>? entryTargetByAbsorbedEntry,
        CancellationToken token)
        => MigrateDelegatesAsync(absorbedIds, targetGroupId, defaultTargetEntryId, entryTargetByAbsorbedEntry, token);

    Task IPublicEntityWriteRules.BroadcastEntityChangeToAllLawyersAsync(
        string kind, string message, string entityOperation, string entityGroupKey, int actorUserId, CancellationToken token)
        => BroadcastEntityChangeToAllLawyersAsync(kind, message, entityOperation, entityGroupKey, actorUserId, token);

    Task IPublicEntityWriteRules.BroadcastToAllHeadsAsync(
        string kind, string message, string entityOperation, string entityGroupKey, int actorUserId, CancellationToken token)
        => BroadcastToAllHeadsAsync(kind, message, entityOperation, entityGroupKey, actorUserId, token);
}
