using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
    // ── الاستيراد التاريخي (د12) ──

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
