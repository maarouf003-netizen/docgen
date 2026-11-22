using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentStatusService : IDocumentStatusService
{
    /// <summary>هل يُتخطى الملف المناب في التوريث/الاسترداد؟ — النهائي (منفذ إنابة)، المشطوب، المسترد، الدرافت.</summary>
    private static bool ExcludedFromDelegationInheritance(Document target) =>
        target.IsDraft
        || target.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
        || target.ExecStatus == ExecutionStatusCatalog.StateStruckOff
        || target.ExecStatus == ExecutionStatusCatalog.Recovered
        || ExecutedStatusCatalog.IsStruckOff(target.ExecutedStatus);

    /// <summary>وقعة تغيير حالة على الملف المناب (تُسجَّل ضمن معاملة المنيب نفسها).</summary>
    private static DocumentOccurrence DelegationTargetOccurrence(Document target, string occurrenceType, string? details) => new()
    {
        DocumentId = target.Id,
        Source = OccurrenceSourceCatalog.System,
        OccurrenceType = occurrenceType,
        EventDate = DateTime.UtcNow,
        Details = details,
        CreatedById = target.CreatedById,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    /// <summary>نسخ كتاب التريث من المنيب إلى المناب + تعليق الحالة + مسح حقول العائلة الأخرى (بلا مبالغ ب6).</summary>
    private static void InheritDeferredInto(Document source, Document target)
    {
        target.TarithNumber = source.TarithNumber;
        target.TarithDate = source.TarithDate;
        target.TarithRegNumber = source.TarithRegNumber;
        target.TarithRegDate = source.TarithRegDate;
        ClearBaraetFields(target);
        ClearSayerFields(target);
        ClearForcedExecutionField(target);
        ClearForcibleTransferFields(target);
        target.ExecSubStatus = null;
        ClearCollectedFields(target);
        target.SoldAssetIds = null;
        target.ExecStatus = ExecutionStatusCatalog.Deferred;
    }

    /// <summary>إحالة المناب إلى «مسترد» (حالة نهائية): تصفير حقول عائلة الحالات بلا مبالغ.</summary>
    private static void RecoverTarget(Document target)
    {
        ClearBaraetFields(target);
        ClearTarithFields(target);
        ClearSayerFields(target);
        ClearForcedExecutionField(target);
        ClearForcibleTransferFields(target);
        target.ExecSubStatus = null;
        ClearCollectedFields(target);
        target.SoldAssetIds = null;
        target.ExecStatus = ExecutionStatusCatalog.Recovered;
    }

    /// <summary>عود الملف المناب إلى المتداول (D3): مسح الحالة وحقول عائلة الحالات كما يراجع المنيب نفسه.</summary>
    private static void RevertTargetStatus(Document target)
    {
        target.ExecStatus = ExecutionStatusCatalog.None;
        target.ExecSubStatus = null;
        ClearCollectedFields(target);
        ClearBaraetFields(target);
        ClearTarithFields(target);
        ClearForcedExecutionField(target);
        ClearForcibleTransferFields(target);
        target.SoldAssetIds = null;
    }

    /// <summary>تفاصيل وقعة «تريث» الموروثة للمناب: حقول كتاب التريث المنقولة من المنيب.</summary>
    private static string? DeferredInheritanceDetails(Document source)
    {
        var details = new Dictionary<string, string>();
        CopyDetail(details, "tarithNumber", source.TarithNumber);
        CopyDetail(details, "tarithDate", source.TarithDate);
        CopyDetail(details, "tarithRegNumber", source.TarithRegNumber);
        CopyDetail(details, "tarithRegDate", source.TarithRegDate);
        return details.Count > 0 ? SerializeDetails(details) : null;
    }

    /// <summary>تفاصيل وقعة «استرداد» (F2): سبب الاسترداد + كتاب براءة الذمة (تسوية) أو تحويل
    /// البدل/رقم الإشعار (اكتمال جبري) + رقم أساس المنيب — بمفاتيح معاودة في نافذة الوقوعات.</summary>
    private static string RecoveryDetails(Document source, bool forcibly, string? sourceFileNumber)
    {
        var details = new Dictionary<string, string>
        {
            ["recoveryReason"] = forcibly ? ExecutionStatusCatalog.ExecutedForcibly : ExecutionStatusCatalog.ExecutedBySettlement,
        };
        if (forcibly)
        {
            CopyDetail(details, "forcibleTransferDate", source.ForcibleTransferDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            CopyDetail(details, "forcibleTransferNoticeNumber", source.ForcibleTransferNoticeNumber);
        }
        else
        {
            CopyDetail(details, "baraetNumber", source.BaraetNumber);
            CopyDetail(details, "baraetDate", source.BaraetDate);
        }
        CopyDetail(details, "sourceFileNumber", sourceFileNumber);
        return SerializeDetails(details);
    }

    /// <summary>تفاصيل وقعة «تراجع» للمناب: حقول كتاب الجهة العامة بالسير بالملف من المنيب —
    /// تُنسخ إلى التفاصيل فقط (تلميع) ولا تُحفظ على المناب.</summary>
    private static Dictionary<string, string> RevertBookDetails(Document source)
    {
        var details = new Dictionary<string, string>();
        CopyDetail(details, "sayerNumber", source.SayerNumber);
        CopyDetail(details, "sayerDate", source.SayerDate);
        CopyDetail(details, "sayerRegNumber", source.SayerRegNumber);
        CopyDetail(details, "sayerRegDate", source.SayerRegDate);
        return details;
    }

    private static void ClearBaraetFields(Document doc)
    {
        doc.BaraetNumber = null;
        doc.BaraetDate = null;
        doc.BaraetRegNumber = null;
        doc.BaraetRegDate = null;
    }

    private static void ClearForcedExecutionField(Document doc)
    {
        doc.ForcedExecutionDate = null;
    }

    /// <summary>
    /// تطهير حقلي «تحويل بدل المبيع» (تاريخ التحويل ورقم الإشعار) عند ترك حالة «منفذ جبريا»
    /// (تسوية/تريث/شطب/تراجع) — يخصان «منفذ جبريا» حصرًا، فيُصفَّران بنفس منطق نظيرهما.
    /// </summary>
    private static void ClearForcibleTransferFields(Document doc)
    {
        doc.ForcibleTransferDate = null;
        doc.ForcibleTransferNoticeNumber = null;
    }

    private static void ClearTarithFields(Document doc)
    {
        doc.TarithNumber = null;
        doc.TarithDate = null;
        doc.TarithRegNumber = null;
        doc.TarithRegDate = null;
    }

    private static void ClearSayerFields(Document doc)
    {
        doc.SayerNumber = null;
        doc.SayerDate = null;
        doc.SayerRegNumber = null;
        doc.SayerRegDate = null;
    }

    /// <summary>
    /// تطهير حقول «محال الى البداية» الأربعة (رقم/تاريخ كتاب المطالعة ورقم/تاريخ كتاب الإحالة)
    /// عند الخروج من الحالة عبر نقطة العودة فقط — الدخول إليها لا يمسح (قرار 11) والتطهير
    /// منشطّر حسب المصدر في UpdateStatusAsync.
    /// </summary>
    private static void ClearReferredToStartFields(Document doc)
    {
        doc.NoFundsDemandNumber = null;
        doc.NoFundsDemandDate = null;
        doc.StartReferralNumber = null;
        doc.StartReferralDate = null;
    }

    private static void ClearCollectedFields(Document doc)
    {
        doc.CollectedAmount = null;
        doc.CollectedAmount2 = null;
        doc.CollectedAmount3 = null;
    }

    private static decimal? ParseCollectedAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        // يوحّد الأرقام العربية/الفارسية ثم فواصل الأرقام العربية (فاصل عشري ٫ وألوف ٬)
        // إلى ما يقبله التحليل؛ فلا يكسر ما يكتب بالأرقام ASCII (يمر كما هو).
        raw = ArabicDigitNormalizer.Normalize(raw)
            .Replace('\u066B', '.')   // ٫ الفاصل العشري العربي
            .Replace('\u066C', ',');  // ٬ فاصل الألوف العربي
        if (decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            || decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out parsed))
        {
            if (parsed < 0)
                throw new ArgumentException("المبلغ المحصل لا يمكن أن يكون سالباً");
            return parsed;
        }
        throw new ArgumentException("المبلغ المحصل غير صالح");
    }

    /// <summary>تطبيق المبالغ المحصلة (حتى ثلاثة بعملاتها) من حقول الطلب على المستند وسجل الوقعة.</summary>
    private static void ApplyCollectedAmounts(Document doc, Dictionary<string, string?> fields, Dictionary<string, string> details)
    {
        doc.CollectedAmount = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount"));
        doc.CollectedAmount2 = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount2"));
        doc.CollectedAmount3 = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount3"));
        var currency = fields.GetValueOrDefault("collectedCurrency");
        var currency2 = fields.GetValueOrDefault("collectedCurrency2");
        var currency3 = fields.GetValueOrDefault("collectedCurrency3");
        doc.CollectedCurrency = string.IsNullOrWhiteSpace(currency) ? "ليرة سورية" : currency.Trim();
        doc.CollectedCurrency2 = string.IsNullOrWhiteSpace(currency2) ? "دولار أمريكي" : currency2.Trim();
        doc.CollectedCurrency3 = string.IsNullOrWhiteSpace(currency3) ? "يورو" : currency3.Trim();
        if (doc.CollectedAmount.HasValue)
        {
            details["collectedAmount"] = doc.CollectedAmount.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency"] = doc.CollectedCurrency;
        }
        if (doc.CollectedAmount2.HasValue)
        {
            details["collectedAmount2"] = doc.CollectedAmount2.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency2"] = doc.CollectedCurrency2;
        }
        if (doc.CollectedAmount3.HasValue)
        {
            details["collectedAmount3"] = doc.CollectedAmount3.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency3"] = doc.CollectedCurrency3;
        }
    }

    /// <summary>
    /// تطبيق الأموال المباعة بالمزاد العلني (إلزامية في «منفذ جبريا»): تُتحقق المعرّفات
    /// من أموال الملف نفسه (عدا كفالة الرواتب)، وتُخزَّن JSON، وتُضمَّن أسماؤها في سجل الوقعة للعرض.
    /// </summary>
    private static void ApplySoldAssets(Document doc, Dictionary<string, string?> fields, Dictionary<string, string> details)
    {
        var raw = (fields.GetValueOrDefault("soldAssetIds") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("اختر الأموال التي جرى بيعها بالمزاد العلني على الأقل");

        var ids = new List<int>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var id))
                throw new ArgumentException("معرّف مال مباع غير صالح");
            ids.Add(id);
        }
        var ownedIds = new HashSet<int>(doc.Assets
            .Where(a => AssetKindCatalog.IsAuctionable(a.AssetKind))
            .Select(a => a.Id));
        if (ids.Any(id => !ownedIds.Contains(id)))
            throw new ArgumentException("الأموال المختارة ليست من أموال الملف");

        doc.SoldAssetIds = SerializeJson(ids);
        details["soldAssetIds"] = string.Join(",", ids);
        var soldNames = doc.Assets
            .Where(a => ids.Contains(a.Id))
            .Select(AssetDisplayName)
            .Where(v => !string.IsNullOrWhiteSpace(v));
        details["soldAssetNames"] = string.Join("، ", soldNames);
    }

    /// <summary>تسمية قراءة للأصل (تُستخدم في «منفذ جبريا» وفي قوائم العرض).</summary>
    private static string AssetDisplayName(Asset a) => AssetDisplay.Label(a);

    private static void CopyDetail(Dictionary<string, string> details, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            details[key] = value;
    }

    private static string SerializeDetails(Dictionary<string, string> details) =>
        JsonSerializer.Serialize(details);

    private static string SerializeJson<T>(T value) => JsonSerializer.Serialize(value);
}
