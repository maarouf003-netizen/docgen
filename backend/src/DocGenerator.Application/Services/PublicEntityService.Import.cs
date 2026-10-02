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
    private static string JoinNameBranch(string? name, string? branch)
        => string.Join(' ', new[] { name, branch }.Where(p => !string.IsNullOrWhiteSpace(p)));
}
