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
    /// <summary>
    /// مزامنة الاسم المجمّد لجهة عامة متدخلة عند إعادة تسمية/دمج/حلول الهوية:
    /// تُطابق بالاسم المعياري القديم (كسائر صفوف الأطراف) وتُعيد العدد المتغير.
    /// </summary>
    private static int SyncPublicInterventionNames(Document doc, string oldNorm, string newCanonical)
    {
        var changed = 0;
        foreach (var intervention in doc.Interventions.Where(i =>
                     i.Nature == PartyNatureCatalog.PublicEntity))
        {
            if (ArabicNameNormalizer.Normalize(intervention.EntityName ?? string.Empty) != oldNorm)
                continue;
            intervention.EntityName = newCanonical;
            changed++;
        }

        return changed;
    }
    // ── مساعدا الطيّ المشتركان (الدمج/النقل-طي/التوحيد) ──
    // دلالة حرفية لمنطق الدمج (المرجع الأكمل): إعادة توجيه روابط القيد الثلاث وإعادة اشتقاق
    // المركّب، ثم الأسماء البديلة (المعياري + الكامل + سوابق القيد الممتصّ) بشروط الاستثناء نفسها.

    /// <summary>يعيد توجيه روابط قيد في مستند إلى قيد آخر ويعيد اشتقاق المركّب.</summary>
    private static void RepointEntryLinks(Document doc, int fromEntryId, int toEntryId)
    {
        foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == fromEntryId))
            a.RegistryId = toEntryId;
        foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == fromEntryId))
            e.RegistryId = toEntryId;
        // المتدخلون الجهات العامة: يتبعون القيد ذاته عند الطيّ/النقل/الدمج،
        // وإلا بقيت روابطهم معلّقة على قيد مُعطَّل/ممتصّ بعد نجاح العملية.
        foreach (var i in doc.Interventions.Where(i =>
                     i.Nature == PartyNatureCatalog.PublicEntity && i.RegistryId == fromEntryId))
            i.RegistryId = toEntryId;
        doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);
    }
    /// <summary>أسماء بديلة «للبحث فقط» على قيد الناجي: المعياري + الكامل + سوابق القيد الممتصّ.</summary>
    private static void AddFoldAliases(PublicEntity targetEntry, string absorbedGroupName, PublicEntity absorbedEntry, ref int aliasesAdded)
    {
        var fullName = $"{absorbedGroupName} — {absorbedEntry.Governorate} / {absorbedEntry.BranchName}";
        var normalizedEntry = ArabicNameNormalizer.Normalize(absorbedGroupName);
        if (!targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normalizedEntry))
        {
            targetEntry.Aliases.Add(new PublicEntityAlias
            {
                PublicEntityId = targetEntry.Id,
                AliasText = absorbedGroupName,
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
        foreach (var priorAlias in absorbedEntry.Aliases)
        {
            var priorNorm = ArabicNameNormalizer.Normalize(priorAlias.AliasText);
            if (priorNorm.Length == 0
                || priorNorm == normalizedEntry
                || priorNorm == normalizedFull
                || targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == priorNorm))
                continue;
            targetEntry.Aliases.Add(new PublicEntityAlias
            {
                PublicEntityId = targetEntry.Id,
                AliasText = priorAlias.AliasText,
            });
            aliasesAdded++;
        }
    }
    internal static int CountUniqueDocuments(Dictionary<int, Document> a, Dictionary<int, Document> b)
    {
        var ids = new HashSet<int>();
        foreach (var k in a.Keys) ids.Add(k);
        foreach (var k in b.Keys) ids.Add(k);
        return ids.Count;
    }
}
