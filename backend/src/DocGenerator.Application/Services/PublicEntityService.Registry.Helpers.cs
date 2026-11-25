using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
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
    internal static PublicEntityEntryDto ToEntryDto(PublicEntityGroup group, PublicEntity entry) => new(
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
        entry.IsParentEntity,
        entry.Version);
}
