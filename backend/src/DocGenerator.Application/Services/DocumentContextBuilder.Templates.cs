using System.Text;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public partial class DocumentContextBuilder : IDocumentContextBuilder
{
    // ── إخطار تنفيذي لكفيل محدد (003 + recipient) — مطابق generate_guarantor_notice ──
    private static void BuildGuarantorNoticeContext(Dictionary<string, object> context, Guarantor guarantor)
    {
        var name = guarantor.GuarantorName ?? string.Empty;
        var address = PrefixedAddress(guarantor.AddressType, guarantor.GuarantorAddress);

        context["borrower_name"] = name;
        context["borrower_father"] = guarantor.GuarantorFather ?? string.Empty;
        context["borrower_family"] = guarantor.GuarantorFamily ?? string.Empty;
        context["borrower_mother"] = guarantor.GuarantorMother ?? string.Empty;
        context["borrower_birth"] = guarantor.GuarantorBirth ?? string.Empty;
        context["borrower_register"] = guarantor.GuarantorRegister ?? string.Empty;
        context["borrower_national_id"] = guarantor.GuarantorNationalId ?? string.Empty;
        context["borrower_address"] = address;

        var full = JoinNonEmpty(new[] { name, guarantor.GuarantorFather, guarantor.GuarantorFamily });
        context["execution_debtor_and_its_adress"] = string.IsNullOrWhiteSpace(address)
            ? full
            : $"{full}\n{address}";
    }

    // ── إخطار بيع أموال غير منقولة (005) — مطابق generate_005_direct ──
    private static void BuildPropertySaleContext(Dictionary<string, object> context, List<Asset> estates)
    {
        var owners = OwnerNames(estates[0]);
        var owner = JoinOwners(owners);

        context["property"] = FormatProperties(estates);
        context["property_owner"] = owner;

        // الاسم الثلاثي للمالك الوحيد يُفكك إلى (الاسم/الأب/العائلة)، وعند تعدد الملاك
        // يُذكر النص المجمع في الاسم ويُترك حقلا الأب والعائلة فارغين.
        if (owners.Count == 1)
        {
            var (ownerName, ownerFather, ownerFamily) = SplitOwnerName(owners[0]);
            context["borrower_name"] = ownerName;
            context["borrower_father"] = ownerFather;
            context["borrower_family"] = ownerFamily;
        }
        else
        {
            context["borrower_name"] = owner;
            context["borrower_father"] = string.Empty;
            context["borrower_family"] = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace((string)context["amount_words"]))
            context["amount_words"] = ((string)context["amount_words"]).Trim() + " مع توابعه القانونية";
    }

    // ── إخطار بيع أموال غير منقولة بالصحف (006) — مطابق generate_006_paper_notice ──
    private static void BuildPropertySalePaperContext(
        Dictionary<string, object> context,
        List<Asset> estates,
        string borrowerFull,
        List<Guarantor> guarantors)
    {
        var owners = OwnerNames(estates[0]);
        var owner = JoinOwners(owners);
        var combined = FormatProperties(estates);

        context["property"] = combined;
        context["property_owner"] = owner;
        context["execution_debtor"] = JoinOwners(owners.Select(o => ResolveOwnerFullName(o, borrowerFull, guarantors)));

        if (!string.IsNullOrWhiteSpace((string)context["amount_words"]))
            context["amount_words"] = ((string)context["amount_words"]).Trim() + " مع توابعه القانونية";
    }

    // ── إخطار تنفيذي بالصحف (007) — مطابق generate_notice_paper_007 ──
    private static void BuildNoticePaperContext(
        Dictionary<string, object> context,
        (string FullName, string Name, string Father, string Family, string Mother, string Birth, string Register, string NationalId, string Address) target,
        int recipient,
        bool isOrdinary)
    {
        context["execution_debtor"] = target.FullName;
        context["recipient_name"] = target.FullName;
        context["recipient_role"] = BuildRecipientRole(recipient, isOrdinary);
        context["borrower_name"] = target.Name;
        context["borrower_father"] = target.Father;
        context["borrower_family"] = target.Family;
        context["borrower_mother"] = target.Mother;
        context["borrower_birth"] = target.Birth;
        context["borrower_register"] = target.Register;
        context["borrower_national_id"] = target.NationalId;
        context["borrower_address"] = target.Address;
        context["contain"] = context["contain_notice"];
    }

    private static string BuildRecipientRole(int recipient, bool isOrdinary)
    {
        if (recipient <= 0)
            return isOrdinary ? "المنفذ عليه الأول" : "المقترض";
        return isOrdinary ? $"المنفذ عليه {recipient + 1}" : $"الكفيل {recipient}";
    }

    // ── حجز عقاري (PS) — مطابق generate_property_seizure ──
    private static void BuildPropertySeizureContext(
        Dictionary<string, object> context,
        Asset estate,
        string borrowerFull,
        List<Guarantor> guarantors)
    {
        var amountWords = (string)context["amount_words"];
        if (amountWords.StartsWith("الزامك بدفع ", StringComparison.Ordinal))
            amountWords = amountWords["الزامك بدفع ".Length..];

        context["Land_Registry"] = estate.LandRegistry ?? string.Empty;
        context["execution_debtor"] = JoinOwners(OwnerNames(estate));
        context["property_number"] = estate.PropertyNumber ?? string.Empty;
        context["property_district"] = estate.PropertyDistrict ?? string.Empty;
        context["execution_debtors"] = BuildExecutionDebtorsPlain(borrowerFull, guarantors);
        context["amount_words"] = amountWords;
        context["estate_seizure_date"] = estate.SeizureDate is null
            ? HandwritingFillLine
            : estate.SeizureDate.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static (string Name, string Father, string Family) SplitOwnerName(string owner)
    {
        var parts = owner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = parts.Length > 0 ? parts[0] : owner;
        var father = parts.Length > 1 ? parts[1] : string.Empty;
        var family = parts.Length > 2 ? string.Join(' ', parts.Skip(2))
            : parts.Length > 1 ? parts[^1] : string.Empty;
        return (name, father, family);
    }

    private static string FormatProperties(List<Asset> estates)
    {
        var texts = estates.Select(e =>
        {
            var number = string.IsNullOrWhiteSpace(e.PropertyNumber) ? "—" : e.PropertyNumber;
            var district = string.IsNullOrWhiteSpace(e.PropertyDistrict) ? "—" : e.PropertyDistrict;
            return e.ShareType == "حصة سهمية"
                ? $"حصتك السهمية بالعقار {number} من المنطقة العقارية {district}"
                : $"تمام عقارك رقم {number} من المنطقة العقارية {district}";
        });
        return string.Join(" و ", texts);
    }

    private static string ResolveOwnerFullName(string owner, string borrowerFull, List<Guarantor> guarantors)
    {
        var borrowerShort = JoinNonEmpty(new[] { ownerFirstName(owner), ownerFamily(owner) });
        // مبسطة: إن كان المالك نصاً مطابقاً للاسم الثلاثي يُستخدم كما هو
        if (owner == borrowerFull)
            return owner;

        foreach (var g in guarantors)
        {
            var gFull = JoinNonEmpty(new[] { g.GuarantorName, g.GuarantorFather, g.GuarantorFamily });
            if (gFull.Length > 0 && owner == gFull)
                return gFull;
        }

        return owner;
    }

    private static string ownerFirstName(string owner)
    {
        var parts = owner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : owner;
    }

    private static string ownerFamily(string owner)
    {
        var parts = owner.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : string.Empty;
    }

    private static (string FullName, string Name, string Father, string Family, string Mother, string Birth, string Register, string NationalId, string Address) ResolveRecipient(
        int recipient, Document doc, List<Guarantor> guarantors)
    {
        if (recipient <= 0)
        {
            return (
                JoinNonEmpty(new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }),
                doc.BorrowerName ?? string.Empty,
                doc.BorrowerFather ?? string.Empty,
                doc.BorrowerFamily ?? string.Empty,
                doc.BorrowerMother ?? string.Empty,
                doc.BorrowerBirth ?? string.Empty,
                doc.BorrowerRegister ?? string.Empty,
                doc.BorrowerNationalId ?? string.Empty,
                doc.BorrowerAddress ?? string.Empty);
        }

        var guarantor = guarantors.FirstOrDefault(g => g.GuarantorNumber == recipient)
            ?? throw new ArgumentException($"كفيل غير موجود: {recipient}");

        return (
            JoinNonEmpty(new[] { guarantor.GuarantorName, guarantor.GuarantorFather, guarantor.GuarantorFamily }),
            guarantor.GuarantorName ?? string.Empty,
            guarantor.GuarantorFather ?? string.Empty,
            guarantor.GuarantorFamily ?? string.Empty,
            guarantor.GuarantorMother ?? string.Empty,
            guarantor.GuarantorBirth ?? string.Empty,
            guarantor.GuarantorRegister ?? string.Empty,
            guarantor.GuarantorNationalId ?? string.Empty,
            guarantor.GuarantorAddress ?? string.Empty);
    }
}
