using System.Text;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public partial class DocumentContextBuilder : IDocumentContextBuilder
{
    private static string XmlEscape(string value) =>
        value.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");

    private static string ToArabicIndicDigits(string value) =>
        string.Concat(value.Select(c => c is >= '0' and <= '9' ? (char)('٠' + (c - '0')) : c));

    private static string JoinNonEmpty(IEnumerable<string?> values) =>
        string.Join(' ', values.Where(v => !string.IsNullOrWhiteSpace(v))).Trim();

    /// <summary>
    /// أسماء ملاك أصل بترتيب الاختيار (من قائمة الملاك الفرعية).
    /// </summary>
    private static List<string> OwnerNames(Asset estate) =>
        estate.Owners.OrderBy(o => o.Order).Select(o => o.Name).ToList();

    /// <summary>
    /// دمج أسماء الملاك المتعددين في نص واحد يفصل بينها « و »، مع تجاهل الفارغ.
    /// </summary>
    private static string JoinOwners(IEnumerable<string> owners) =>
        string.Join(" و ", owners.Where(o => !string.IsNullOrWhiteSpace(o)));

    private static string BuildExecutionDebtorsPlain(string borrowerFull, List<Guarantor> guarantors)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(borrowerFull))
            names.Add(borrowerFull);

        foreach (var guarantor in guarantors.OrderBy(g => g.GuarantorNumber))
        {
            var full = JoinNonEmpty(new[] { guarantor.GuarantorName, guarantor.GuarantorFather, guarantor.GuarantorFamily });
            if (full.Length > 0)
                names.Add(full);
        }

        return string.Join('\n', names);
    }

    private static List<Asset> SelectEstates(List<Asset> estates, int[]? estateIds)
    {
        if (estateIds is null || estateIds.Length == 0)
            throw new ArgumentException("يرجى اختيار عقار واحد على الأقل");

        var selected = estates.Where(e => estateIds.Contains(e.Id)).ToList();
        if (selected.Count == 0)
            throw new ArgumentException("العقار المحدد غير موجود");

        return selected;
    }

    /// <summary>
    /// السابقة النصية لحقل العنوان المجرد حسب نوع العنوان: «موطناً مختاراً » للموطن المختار،
    /// «يمثله » عندما يكون الطرف ممثَّلًا بوكيل، ولا سابقة لبقية الأنواع.
    /// </summary>
    private static string AddressPrefixOf(string? addressType) => addressType switch
    {
        "موطن مختار" => "موطناً مختاراً ",
        "يمثله" => "يمثله ",
        _ => string.Empty,
    };

    /// <summary>
    /// يعيد العنوان مع سابقته المناسبة لنوعه إذا كان العنوان غير فارغ؛ وإلا يعيد سلسلة فارغة
    /// دون ذكر أي سابقة مهما كان نوع العنوان.
    /// </summary>
    private static string PrefixedAddress(string? addressType, string? address)
    {
        var value = (address ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : AddressPrefixOf(addressType) + value;
    }

    /// <summary>
    /// صيغة جملة عنوان/وكيل الطرف في المستندات: «متخذا موطنا مختارا …» للموطن المختار،
    /// «يمثله …» عندما يكون الطرف ممثَّلًا، و«عنوانه …» لبقية الأنواع. علامة (withColon)
    /// تحافظ على الصيغتين التاريخيتين «متخذا موطنا مختارا: …» و«متخذا موطنا مختارا …».
    /// </summary>
    private static string AddressPhraseOf(string? addressType, string address, bool withColon)
    {
        if (addressType == "يمثله")
            return $"يمثله {address}";
        return addressType == "موطن مختار"
            ? withColon
                ? $"متخذا موطنا مختارا: {address}"
                : $"متخذا موطنا مختارا {address}"
            : $"عنوانه {address}";
    }
}
