using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// مساعدات كتابة سجل الجهات العامة المشتركة بين الخدمة وأوامر `P4-01be` — تبقى
/// التنفيذات في `PublicEntityService` (مصدر واحد) ويستهلكها المعالج عبر هذا العقد
/// الداخلي: الحرّاس، والتحقق من التكرار، وأحداث التغيير، ومزامنة إعادة التسمية.
/// لا يفتح أي عضو معاملة؛ المعاملة ملك المعالج.
/// </summary>
internal interface IPublicEntityWriteRules
{
    Task EnsureHeadScopeAsync(EntityRegistryActor actor, PublicEntity? entry, string? fallbackGovernorate, CancellationToken ct);
    Task EnsureCanonicalAvailableAsync(string canonical, int excludeGroupId, CancellationToken ct);
    Task EnsureNoDuplicateEntryAsync(int? excludeEntryId, string canonical, string governorate, string branchName, CancellationToken ct);
    Task<PublicEntityGroup> FindOrCreateGroupAsync(string canonical, string entityType, int actorUserId, CancellationToken token);
    Task TrackChangeEventAsync(PublicEntityChangeEvent changeEvent, CancellationToken token);
    Task<List<Document>> SyncTextsAfterRenameAsync(string oldCanonical, string newCanonical, string? actorName, CancellationToken token);
    Task SyncAppealsAfterEntityChangeAsync(IReadOnlyCollection<Document> affectedDocs, EntityRegistryActor actor, CancellationToken token);
    Task InsertRenameNoticeToCreatorAsync(PublicEntity entry, string oldName, string newName, CancellationToken token);
    PublicEntityEntryDto ToEntryDto(PublicEntityGroup group, PublicEntity entry);
    string Required(string? value, string emptyMessage, int maxLength);
    string RequiredWithFallback(string? value, string fallback, int maxLength);
    string ValidEntityType(string? value);
    string ValidCitationFormula(string? value, string fallback);
    List<string> CleanAliases(IEnumerable<string>? aliases, string canonicalNorm);
    string? ValidateCoverageLabel(string? value);
    string? NormalizeOptional(string? value);
    void SetGroupNorm(PublicEntityGroup group);
    void GuardHeadCannotEditParent(EntityRegistryActor actor, PublicEntity entry);
    Task<List<ImportPreviewItemDto>> BuildImportCandidatesAsync(CancellationToken ct);
    Task<PublicEntityGroup?> FindGroupByNormAsync(string norm, CancellationToken token);
}
