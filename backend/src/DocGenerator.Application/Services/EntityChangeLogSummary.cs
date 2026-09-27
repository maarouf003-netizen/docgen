using System.Text.Json;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// ملخّص عربي قابل للعرض لسطر في سجل تغييرات الجهات — يُبنى وقت القراءة من
/// <c>PublicEntityChangeEvent.PayloadJson</c> (الحقل الخام المحفوظ بلا أي مساس).
/// الصياغة الجاهزة تُستعار من <see cref="EntityChangeMessages"/> للأصناف المغطاة
/// (إعادة تسمية/نقل/دمج/توحيد/حلول) كي تبقى العربية مصدرًا واحدًا؛ وصياغات
/// الأصناف الأخرى (اقتراح/تحديث) محلية هنا لأن لا قناة واقعة/تنبيه لها.
/// الحمل التالف أو المجهول يُطبَّع إلى تسمية الصنف فقط — لا يُسقط الشاشة أبدًا.
/// </summary>
public static class EntityChangeLogSummary
{
    /// <summary>يبني الملخّص العربي لحدث تغيير واحد. لا يرمي أبدًا.</summary>
    public static string Build(
        string actionKind,
        string? payloadJson,
        string? decreeKind,
        string? decreeNumber,
        DateTime? decreeDate)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return ActionKindCatalog.ToLabel(actionKind ?? string.Empty);

        Dictionary<string, JsonElement> fields;
        try
        {
            fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson)
                ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return ActionKindCatalog.ToLabel(actionKind ?? string.Empty);
        }

        var summary = actionKind switch
        {
            ActionKindCatalog.Rename => RenameSummary(fields, decreeKind, decreeNumber, decreeDate),
            ActionKindCatalog.Update => UpdateSummary(fields, decreeKind, decreeNumber, decreeDate),
            ActionKindCatalog.Move => MoveSummary(fields, decreeKind, decreeNumber, decreeDate),
            ActionKindCatalog.Merge => MergeSummary(fields, decreeKind, decreeNumber, decreeDate),
            ActionKindCatalog.Unify => UnifySummary(fields, decreeKind, decreeNumber, decreeDate),
            ActionKindCatalog.Abolish => AbolishSummary(fields, decreeKind, decreeNumber, decreeDate),
            ActionKindCatalog.Propose => ProposeSummary(fields),
            _ => (string?)null,
        };
        return string.IsNullOrWhiteSpace(summary)
            ? ActionKindCatalog.ToLabel(actionKind ?? string.Empty)
            : summary;
    }

    // ── استخراج دفاعي ──

    private static string? Text(IReadOnlyDictionary<string, JsonElement> fields, string key)
    {
        if (!fields.TryGetValue(key, out var element))
            return null;
        var value = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyList<string> Texts(IReadOnlyDictionary<string, JsonElement> fields, string key)
    {
        if (!fields.TryGetValue(key, out var element))
            return Array.Empty<string>();
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToList();
        }
        var single = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        return string.IsNullOrWhiteSpace(single) ? Array.Empty<string>() : new[] { single.Trim() };
    }

    private static long? Count(IReadOnlyDictionary<string, JsonElement> fields, string key)
    {
        if (!fields.TryGetValue(key, out var element))
            return null;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var value)
            ? value
            : null;
    }

    // ── تركيب ──

    private static string JoinParts(params string?[] parts)
        => string.Join(" — ", parts.Where(part => !string.IsNullOrWhiteSpace(part))!);

    private static string WithDecree(string message, string? decreeKind, string? decreeNumber, DateTime? decreeDate)
    {
        var suffix = EntityChangeMessages.DecreeSuffix(decreeKind ?? string.Empty, decreeNumber ?? string.Empty, decreeDate);
        return string.IsNullOrWhiteSpace(suffix) ? message : $"{message} بموجب {suffix}";
    }

    /// <summary>أزواج قبل/بعد المتغيّرة فعلًا: «المحافظة: «أ» ← «ب»» مفصولة بـ«؛».</summary>
    private static string? DescribePairs(
        IReadOnlyDictionary<string, JsonElement> fields,
        params (string OldKey, string NewKey, string Label)[] pairs)
    {
        var parts = new List<string>();
        foreach (var (oldKey, newKey, label) in pairs)
        {
            var oldValue = Text(fields, oldKey);
            var newValue = Text(fields, newKey);
            if (oldValue is null || newValue is null)
                continue;
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                continue;
            parts.Add($"{label}: «{oldValue}» ← «{newValue}»");
        }
        return parts.Count == 0 ? null : string.Join("؛ ", parts);
    }

    private static string? DescribeCounts(
        IReadOnlyDictionary<string, JsonElement> fields,
        params (string Key, string Unit)[] counts)
    {
        var parts = new List<string>();
        foreach (var (key, unit) in counts)
        {
            var value = Count(fields, key);
            if (value.HasValue)
                parts.Add($"{value.Value} {unit}");
        }
        return parts.Count == 0 ? null : string.Join("، ", parts);
    }

    // ── الأصناف ──

    private static string? RenameSummary(
        IReadOnlyDictionary<string, JsonElement> fields,
        string? decreeKind, string? decreeNumber, DateTime? decreeDate)
    {
        // شكل الهوية الأم (RenameGroupAsync) يخزّن القديم جمعًا بلا مفرد.
        var oldName = Text(fields, "oldCanonical")
            ?? Texts(fields, "oldCanonicalNames").FirstOrDefault();
        var newName = Text(fields, "newCanonical");
        if (oldName is null || newName is null)
            return null;
        var head = string.Equals(oldName, newName, StringComparison.Ordinal)
            ? WithDecree($"تعديل بيانات «{newName}»", decreeKind, decreeNumber, decreeDate)
            : EntityChangeMessages.RenameOccurrence(oldName, newName, decreeKind ?? string.Empty, decreeNumber ?? string.Empty, decreeDate);
        var extra = DescribePairs(fields,
            ("oldBranchName", "newBranchName", "الفرع"),
            ("oldGovernorate", "newGovernorate", "المحافظة"),
            ("oldBranch", "newBranch", "الفرع"));
        var counts = DescribeCounts(fields, ("affectedDocuments", "ملفًا"));
        return JoinParts(head, extra, counts);
    }

    private static string? UpdateSummary(
        IReadOnlyDictionary<string, JsonElement> fields,
        string? decreeKind, string? decreeNumber, DateTime? decreeDate)
    {
        var pairs = DescribePairs(fields,
            ("oldCanonical", "newCanonical", "التسمية"),
            ("oldBranchName", "newBranchName", "الفرع"),
            ("oldGovernorate", "newGovernorate", "المحافظة"),
            ("oldBranch", "newBranch", "الفرع"));
        return pairs is null
            ? null
            : WithDecree($"تحديث بيانات: {pairs}", decreeKind, decreeNumber, decreeDate);
    }

    private static string? MoveSummary(
        IReadOnlyDictionary<string, JsonElement> fields,
        string? decreeKind, string? decreeNumber, DateTime? decreeDate)
    {
        var from = Text(fields, "fromGroup");
        var to = Text(fields, "toGroup");
        if (from is null || to is null)
            return null;
        var text = EntityChangeMessages.MoveAllOccurrence(from, to, decreeKind ?? string.Empty, decreeNumber ?? string.Empty, decreeDate);
        var counts = DescribeCounts(fields, ("entriesMoved", "قيدًا"), ("affectedDocuments", "ملفًا"));
        var note = Text(fields, "note");
        return JoinParts(text, counts, note is null ? null : $"ملاحظة: {note}");
    }

    private static string? MergeSummary(
        IReadOnlyDictionary<string, JsonElement> fields,
        string? decreeKind, string? decreeNumber, DateTime? decreeDate)
    {
        var absorbed = Texts(fields, "oldCanonicalNames");
        var target = Text(fields, "survivorGroup")
            ?? Text(fields, "newCanonical")
            ?? Text(fields, "targetGroup");
        if (absorbed.Count == 0 || target is null)
            return null;
        var text = EntityChangeMessages.MergeOccurrence(
            string.Join("، ", absorbed), target,
            decreeKind ?? string.Empty, decreeNumber ?? string.Empty, decreeDate);
        var documents = Count(fields, "totalAffectedDocs") ?? Count(fields, "affectedDocuments");
        var parts = new List<string?>
        {
            DescribeCounts(fields, ("entriesMigrated", "قيدًا"), ("aliasesAdded", "اسمًا بديلًا")),
            documents.HasValue ? $"{documents.Value} ملفًا" : null,
        };
        return JoinParts(new[] { text }.Concat(parts).ToArray());
    }

    private static string? UnifySummary(
        IReadOnlyDictionary<string, JsonElement> fields,
        string? decreeKind, string? decreeNumber, DateTime? decreeDate)
    {
        var absorbed = Texts(fields, "oldCanonicalNames");
        if (absorbed.Count == 0)
            absorbed = Texts(fields, "absorbedBranchNames");
        var target = Text(fields, "targetGroup")
            ?? Text(fields, "newCanonical")
            ?? Text(fields, "correctedName");
        if (absorbed.Count == 0 || target is null)
            return null;
        var text = EntityChangeMessages.UnifyOccurrence(
            string.Join("، ", absorbed), target,
            decreeKind ?? string.Empty, decreeNumber ?? string.Empty, decreeDate);
        var documents = Count(fields, "totalAffectedDocs") ?? Count(fields, "affectedDocuments");
        var parts = new List<string?>
        {
            DescribeCounts(fields,
                ("entriesMoved", "قيدًا"),
                ("entriesFolded", "قيدًا"),
                ("groupsUnified", "هويةً"),
                ("aliasesAdded", "اسمًا بديلًا")),
            documents.HasValue ? $"{documents.Value} ملفًا" : null,
        };
        return JoinParts(new[] { text }.Concat(parts).ToArray());
    }

    private static string? AbolishSummary(
        IReadOnlyDictionary<string, JsonElement> fields,
        string? decreeKind, string? decreeNumber, DateTime? decreeDate)
    {
        var abolished = Texts(fields, "oldCanonicalNames");
        var replacement = Text(fields, "newCanonical");
        if (abolished.Count == 0 || replacement is null)
            return null;
        var text = EntityChangeMessages.AbolishOccurrence(
            replacement, string.Join("، ", abolished),
            decreeKind ?? string.Empty, decreeNumber ?? string.Empty, decreeDate);
        var parts = new List<string?>
        {
            DescribeCounts(fields,
                ("entriesMoved", "قيدًا"),
                ("affectedDocuments", "ملفًا"),
                ("delegatesReassigned", "مندوبًا")),
        };
        return JoinParts(new[] { text }.Concat(parts).ToArray());
    }

    private static string? ProposeSummary(IReadOnlyDictionary<string, JsonElement> fields)
    {
        var current = Text(fields, "canonicalName") ?? Text(fields, "oldCanonical");
        var proposed = Text(fields, "proposedCanonicalName") ?? Text(fields, "newCanonical");
        string? head = (current, proposed) switch
        {
            (not null, not null) => $"اقتراح تعديل «{current}» ← «{proposed}»",
            (not null, null) => $"اقتراح تعديل جهة: «{current}»",
            (null, not null) => $"اقتراح تعديل جهة: «{proposed}»",
            _ => null,
        };
        var pairs = DescribePairs(fields,
            ("oldGovernorate", "newGovernorate", "المحافظة"),
            ("oldBranch", "newBranch", "الفرع"));
        var coverage = Text(fields, "coverageLabel");
        var reason = Text(fields, "reason");
        var parts = JoinParts(head, pairs,
            coverage is null ? null : $"التغطية: «{coverage}»",
            reason is null ? null : $"السبب: {reason}");
        return string.IsNullOrWhiteSpace(parts) ? null : parts;
    }
}
