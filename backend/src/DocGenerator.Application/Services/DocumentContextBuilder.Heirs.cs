using System.Text;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public partial class DocumentContextBuilder : IDocumentContextBuilder
{
    private static string BuildBorrowerAddressRichXml(string borrowerFull, string addressText) =>
        BuildRichPairXml(borrowerFull, addressText);

    /// <summary>
    /// زوج (اسم عريض + عنوان عادي) بصيغة RichText صالحة داخل فقرة Word.
    /// </summary>
    private static string BuildRichPairXml(string name, string addressText)
    {
        if (string.IsNullOrWhiteSpace(name))
            return addressText;

        var xml = $"<w:r><w:rPr><w:b/><w:rtl/></w:rPr><w:t xml:space=\"preserve\">{XmlEscape(name)}</w:t></w:r>";
        if (!string.IsNullOrWhiteSpace(addressText))
            xml += $"<w:r><w:rPr><w:rtl/></w:rPr><w:t xml:space=\"preserve\"> {XmlEscape(addressText)}</w:t></w:r>";
        return xml;
    }

    // ── ورثة المنفذ عليهم المتوفين ──

    /// <summary>ورثة منفذ عليه محدد (GuarantorNumber = null يعني ورثة المقترض).</summary>
    private static List<Heir> HeirsOf(IEnumerable<Heir> heirs, int? guarantorNumber) =>
        heirs.Where(h => h.GuarantorNumber == guarantorNumber).ToList();

    /// <summary>
    /// عبارة عنوان/وكيل الوريث: «عنوانه …» أو «يمثله …». إذا كان الحقل فارغًا تُلغى
    /// السابقة كليًا فيُذكر الاسم الثلاثي للوريث فقط. عند اختيار «وكيل» تُستخدم صيغة
    /// «يمثله …» دون تكرار إن كانت القيمة المخزنة تحمل السابقة ذاتها.
    /// </summary>
    private static string HeirAddressPhrase(Heir heir)
    {
        var value = (heir.HeirAddress ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        if (heir.AddressType != "وكيل")
            return $"عنوانه {value}";

        if (value.StartsWith("يمثله", StringComparison.Ordinal))
        {
            var attorney = value["يمثله".Length..].TrimStart();
            if (string.IsNullOrWhiteSpace(attorney))
                return string.Empty;
            value = attorney;
        }

        return $"يمثله {value}";
    }

    /// <summary>
    /// عبارة عنوان الممثل الشرعي للوريث حسب نوعه: «موطنه المختار…» أو «عنوانه…» أو
    /// «وكيله القانوني…». تُعاد فارغة عند غياب العنوان.
    /// </summary>
    private static string HeirRepresentativeAddressPhrase(Heir heir)
    {
        var value = (heir.RepresentativeAddress ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return (heir.RepresentativeAddressType ?? string.Empty).Trim() switch
        {
            "موطن مختار" => $"موطنه المختار {value}",
            "وكيل قانوني" => $"وكيله القانوني {value}",
            _ => $"عنوانه {value}",
        };
    }

    /// <summary>
    /// عبارة الممثل الشرعي للوريث: «ممثله الشرعي الولي …» مع عنوانه عند الطلب.
    /// تُعاد فارغة عند غياب الاسم الثلاثي (فلا يُخزَّن ممثل بلا اسم).
    /// </summary>
    private static string HeirRepresentativePhrase(Heir heir, bool withAddress)
    {
        if (DocumentText.IsEmptyRepresentative(
                heir.RepresentativeName, heir.RepresentativeFather, heir.RepresentativeFamily))
            return string.Empty;

        var name = JoinNonEmpty(new[] { heir.RepresentativeName, heir.RepresentativeFather, heir.RepresentativeFamily });
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var capacity = (heir.RepresentativeCapacity ?? string.Empty).Trim();
        var phrase = capacity.Length > 0 ? $"ممثله الشرعي ال{capacity} {name}" : $"ممثله الشرعي {name}";
        if (!withAddress)
            return phrase;

        var address = HeirRepresentativeAddressPhrase(heir);
        return string.IsNullOrWhiteSpace(address) ? phrase : $"{phrase}، {address}";
    }

    /// <summary>عبارة الطرف الوريث: ممثله الشرعي إن وُجد، وإلا عنوانه/وكيله المخزَّن.</summary>
    private static string HeirPartyPhrase(Heir heir, bool withAddress)
    {
        var representative = HeirRepresentativePhrase(heir, withAddress);
        if (!string.IsNullOrWhiteSpace(representative))
            return representative;
        return withAddress ? HeirAddressPhrase(heir) : string.Empty;
    }

    /// <summary>
    /// سطر الوريث في الإخطارات: «[الوريث] إضافة لتركة المتوفى ([المتوفى])». مع
    /// (withAddress) تُلحق عبارة العنوان/الوكيل أو الممثل الشرعي في سطر تالٍ إن وُجدت؛
    /// وفي إخطارات الصحف (006/007) يُذكر اسم الممثل الشرعي دون عنوان.
    /// </summary>
    private static string BuildHeirLine(Heir heir, string deceasedFull, bool withAddress)
    {
        var line = $"{JoinNonEmpty(new[] { heir.HeirName, heir.HeirFather, heir.HeirFamily })} إضافة لتركة المتوفى ({deceasedFull})";
        var phrase = HeirPartyPhrase(heir, withAddress);
        return string.IsNullOrWhiteSpace(phrase) ? line : $"{line}\n{phrase}";
    }

    /// <summary>
    /// كتلة «ورثة المتوفى» لقوائم الاستدعاء والمحضر (001/002): الاسم العريض
    /// «ورثة المتوفى [المتوفى]» تليه العبارة «وهم: [قائمة الورثة] إضافة لتركة مورثهم ([المتوفى])».
    /// </summary>
    private static (string Name, string Address) BuildHeirsRichItem(string deceasedFull, List<Heir> heirs)
    {
        var list = heirs
            .Select(h =>
            {
                var name = JoinNonEmpty(new[] { h.HeirName, h.HeirFather, h.HeirFamily });
                if (string.IsNullOrWhiteSpace(name))
                    return string.Empty;
                var phrase = HeirPartyPhrase(h, withAddress: true);
                return string.IsNullOrWhiteSpace(phrase) ? name : $"{name}، {phrase}";
            })
            .Where(p => p.Length > 0)
            .ToList();

        var joined = string.Join("، ", list);
        if (string.IsNullOrWhiteSpace(joined))
            return (deceasedFull, string.Empty);

        return (
            $"ورثة المتوفى {deceasedFull}",
            $"وهم: {joined} إضافة لتركة مورثهم ({deceasedFull})");
    }

    /// <summary>
    /// يحل الوريث بمعرفه ويعيده مع الاسم الثلاثي لمورثه (المقترض أو الكفيل صاحب الوريث).
    /// </summary>
    private static (Heir Heir, string DeceasedFullName) ResolveHeirContext(
        IEnumerable<Heir> heirs,
        int heirId,
        string borrowerFull,
        List<Guarantor> guarantors)
    {
        var heir = heirs.FirstOrDefault(h => h.Id == heirId)
            ?? throw new ArgumentException("الوريث المحدد غير موجود");

        string deceasedFull;
        if (heir.GuarantorNumber is null)
        {
            deceasedFull = borrowerFull;
        }
        else
        {
            var guarantor = guarantors.FirstOrDefault(g => g.GuarantorNumber == heir.GuarantorNumber);
            deceasedFull = JoinNonEmpty(new[]
            {
                guarantor?.GuarantorName, guarantor?.GuarantorFather, guarantor?.GuarantorFamily
            });
        }

        return (heir, deceasedFull);
    }

    /// <summary>إخطار تنفيذي موجَّه إلى وريث (003 + heirId): يذكر المتوفى مع العنوان/الوكيل.</summary>
    private static void BuildHeirNoticeContext(
        Dictionary<string, object> context,
        (Heir Heir, string DeceasedFullName) resolved)
    {
        context["borrower_name"] = JoinNonEmpty(new[] { resolved.Heir.HeirName, resolved.Heir.HeirFather, resolved.Heir.HeirFamily });
        context["execution_debtor_and_its_adress"] =
            BuildHeirLine(resolved.Heir, resolved.DeceasedFullName, withAddress: true);
    }

    /// <summary>إخطار تنفيذي بالصحف موجَّه إلى وريث (007 + heirId): بلا عنوان ولا وكيل.</summary>
    private static void BuildHeirPaperContext(
        Dictionary<string, object> context,
        (Heir Heir, string DeceasedFullName) resolved)
    {
        var heirName = JoinNonEmpty(new[] { resolved.Heir.HeirName, resolved.Heir.HeirFather, resolved.Heir.HeirFamily });
        context["execution_debtor"] = BuildHeirLine(resolved.Heir, resolved.DeceasedFullName, withAddress: false);
        context["recipient_name"] = heirName;
    }

    private static string BuildDebtorsRichXml(
        string borrowerFull,
        string borrowerAddress,
        string? borrowerAddressType,
        List<Guarantor> guarantors,
        List<Heir> heirs)
    {
        var items = new List<(string Name, string Address)>();

        var borrowerHeirs = HeirsOf(heirs, null);
        if (borrowerHeirs.Count > 0)
        {
            items.Add(BuildHeirsRichItem(borrowerFull, borrowerHeirs));
        }
        else if (!string.IsNullOrWhiteSpace(borrowerFull))
        {
            var address = string.Empty;
            if (!string.IsNullOrWhiteSpace(borrowerAddress))
                address = AddressPhraseOf(borrowerAddressType, borrowerAddress, withColon: false);
            items.Add((borrowerFull, address));
        }

        // كل الكفلاء المخزنين بترتيب أرقامهم المستقرة (بلا سقف مرقم ثابت).
        foreach (var guarantor in guarantors.OrderBy(g => g.GuarantorNumber))
        {
            var name = string.Join(' ', new[]
            {
                guarantor.GuarantorName, guarantor.GuarantorFather, guarantor.GuarantorFamily
            }.Where(v => !string.IsNullOrWhiteSpace(v)));

            if (string.IsNullOrWhiteSpace(name))
                continue;

            var guarantorHeirs = HeirsOf(heirs, guarantor.GuarantorNumber);
            if (guarantorHeirs.Count > 0)
            {
                items.Add(BuildHeirsRichItem(name, guarantorHeirs));
                continue;
            }

            var address = string.Empty;
            if (!string.IsNullOrWhiteSpace(guarantor.GuarantorAddress))
                address = AddressPhraseOf(guarantor.AddressType, guarantor.GuarantorAddress, withColon: false);
            items.Add((name, address));
        }

        var sb = new StringBuilder();
        for (var idx = 0; idx < items.Count; idx++)
        {
            if (idx > 0)
                sb.Append("<w:r><w:rPr><w:rtl/></w:rPr><w:br/></w:r>");
            sb.Append($"<w:r><w:rPr><w:b/><w:rtl/></w:rPr><w:t xml:space=\"preserve\">{XmlEscape(items[idx].Name)}</w:t></w:r>");
            if (!string.IsNullOrWhiteSpace(items[idx].Address))
                sb.Append($"<w:r><w:rPr><w:rtl/></w:rPr><w:t xml:space=\"preserve\"> {XmlEscape(items[idx].Address)}</w:t></w:r>");
        }

        return sb.ToString();
    }
}
