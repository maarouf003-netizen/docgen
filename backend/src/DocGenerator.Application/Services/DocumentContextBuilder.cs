using System.Text;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// يبني سياق قالب docx من بيانات المستند الحيّة (المكافئ لـ prepare_docxtpl_context + build_document_context).
/// </summary>
public class DocumentContextBuilder : IDocumentContextBuilder
{
    private readonly IRepository<Document> _documents;

    public DocumentContextBuilder(IRepository<Document> documents) => _documents = documents;

    public async Task<Dictionary<string, object>> BuildContextAsync(
        int documentId,
        string templateCode,
        int recipient = 0,
        int[]? estateIds = null,
        CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new KeyNotFoundException($"المستند غير موجود: {documentId}");

        var context = new Dictionary<string, object>(StringComparer.Ordinal);
        var contractTypeSelector = string.IsNullOrWhiteSpace(doc.ContractTypeSelector)
            ? "مصرفي"
            : doc.ContractTypeSelector;
        var isOrdinary = contractTypeSelector == "عادي";

        var contractType = doc.ContractType ?? string.Empty;
        if (isOrdinary && !string.IsNullOrWhiteSpace(contractType) && !contractType.StartsWith("قرار "))
            contractType = $"قرار {contractType}";

        context["court"] = doc.Court ?? string.Empty;
        context["court_with_prefix"] = $"دائرة تنفيذ {doc.Court}".Trim();
        context["lawyer"] = doc.Lawyer ?? string.Empty;
        context["borrower_name"] = doc.BorrowerName ?? string.Empty;
        context["borrower_father"] = doc.BorrowerFather ?? string.Empty;
        context["borrower_family"] = doc.BorrowerFamily ?? string.Empty;
        context["borrower_mother"] = doc.BorrowerMother ?? string.Empty;
        context["borrower_birth"] = doc.BorrowerBirth ?? string.Empty;
        context["borrower_register"] = doc.BorrowerRegister ?? string.Empty;
        context["borrower_national_id"] = doc.BorrowerNationalId ?? string.Empty;
        context["contract_type"] = contractType;
        context["contract_number"] = doc.ContractNumber ?? string.Empty;
        context["contract_date"] = doc.ContractDate ?? string.Empty;
        context["amount_numeric"] = doc.AmountNumeric;
        context["amount_words"] = doc.AmountWords ?? string.Empty;
        context["current_date"] = DateTime.Today.ToString("dd/MM/yyyy");
        context["current_date_arabic"] = ToArabicIndicDigits(DateTime.Today.ToString("dd/MM/yyyy"));
        context["currency"] = doc.Currency ?? "ليرة سورية";
        context["contract_type_selector"] = contractTypeSelector;
        context["file_number"] = doc.FileNumber ?? string.Empty;
        context["file_type"] = doc.FileType ?? string.Empty;
        context["file_year"] = doc.FileYear ?? string.Empty;
        context["file_number_full"] = string.IsNullOrWhiteSpace(doc.FileYear)
            ? doc.FileNumber ?? string.Empty
            : $"{doc.FileNumber}/{doc.FileYear}";
        context["immediate_actions"] = doc.ImmediateActions ?? string.Empty;
        context["immediate_actions_prefix"] = string.IsNullOrWhiteSpace(doc.ImmediateActions)
            ? string.Empty
            : "تم تضمين الشق الأول ما تم من إجراءات";

        // المنفذ عليه مع عنوانه (نص عادي متعدد الأسطر)
        var borrowerFull = string.Join(' ', new[]
        {
            doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily
        }.Where(v => !string.IsNullOrWhiteSpace(v)));
        var borrowerAddress = (doc.BorrowerAddress ?? string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(borrowerFull))
        {
            if (!string.IsNullOrWhiteSpace(borrowerAddress))
            {
                context["execution_debtor_and_its_adress"] = doc.BorrowerAddressType == "موطن مختار"
                    ? $"{borrowerFull}\nمتخذا موطنا مختارا: {borrowerAddress}"
                    : $"{borrowerFull}\nعنوانه {borrowerAddress}";
            }
            else
            {
                context["execution_debtor_and_its_adress"] = borrowerFull;
            }
        }
        else
        {
            context["execution_debtor_and_its_adress"] = string.Empty;
        }

        var addressPrefix = doc.BorrowerAddressType == "موطن مختار" ? "موطناً مختاراً " : string.Empty;
        context["borrower_address"] = addressPrefix + (doc.BorrowerAddress ?? string.Empty);
        context["borrower_address_type"] = doc.BorrowerAddressType ?? "موطن مختار";

        var applicant = doc.Applicant ?? string.Empty;
        context["applicant"] = applicant.EndsWith("إضافة لوظيفته، تمثله إدارة قضايا الدولة")
            ? applicant
            : $"{applicant} إضافة لوظيفته، تمثله إدارة قضايا الدولة";
        context["raw_applicant"] = applicant;

        // contain / contain_notice / contain1
        if (!isOrdinary)
        {
            var original = doc.AmountWords ?? string.Empty;
            context["contain"] = string.IsNullOrEmpty(original)
                ? string.Empty
                : $"التزام الجهة المنفذ عليها بدفع مبلغ {original} مع توابعه القانونية";
            context["contain_notice"] = string.IsNullOrEmpty(original)
                ? string.Empty
                : $"الزامك بدفع مبلغ {original} مع توابعه القانونية";
            context["contain1"] = string.IsNullOrEmpty(original)
                ? string.Empty
                : $"الزامي بدفع مبلغ {original} مع توابعه القانونية";
            context["amount_words"] = original;
            context["amount_words_record"] = original;
            context["amount_words_record_execution"] = context["contain"];
            context["amount_words_notice"] = context["contain_notice"];
        }
        else
        {
            var inclusionText = doc.InclusionText ?? string.Empty;
            var inclusionAmount = doc.InclusionAmountWords ?? string.Empty;
            string containValue;
            if (!string.IsNullOrWhiteSpace(inclusionText) && !string.IsNullOrWhiteSpace(inclusionAmount))
                containValue = $"{inclusionText} و دفع مبلغ {inclusionAmount} مع فوائده القانونية";
            else if (!string.IsNullOrWhiteSpace(inclusionText))
                containValue = inclusionText;
            else if (!string.IsNullOrWhiteSpace(inclusionAmount))
                containValue = $"دفع مبلغ {inclusionAmount} مع فوائده القانونية";
            else
                containValue = string.Empty;

            context["contain"] = containValue;
            context["contain_notice"] = containValue;
            context["contain1"] = containValue;
            context["amount_words"] = string.Empty;
            context["amount_words_record"] = containValue;
            context["amount_words_record_execution"] = containValue;
            context["amount_words_notice"] = containValue;
        }

        // الكفلاء (1 إلى 5)
        var guarantors = doc.Guarantors.OrderBy(g => g.GuarantorNumber).ToList();
        for (var i = 1; i <= 5; i++)
        {
            var guarantor = guarantors.FirstOrDefault(g => g.GuarantorNumber == i);
            var name = guarantor?.GuarantorName ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(name))
            {
                var gAddressPrefix = guarantor?.AddressType == "موطن مختار" ? "موطناً مختاراً " : string.Empty;
                context[$"guarantor_{i}_name"] = name;
                context[$"guarantor_{i}_father"] = guarantor?.GuarantorFather ?? string.Empty;
                context[$"guarantor_{i}_family"] = guarantor?.GuarantorFamily ?? string.Empty;
                context[$"guarantor_{i}_mother"] = guarantor?.GuarantorMother ?? string.Empty;
                context[$"guarantor_{i}_birth"] = guarantor?.GuarantorBirth ?? string.Empty;
                context[$"guarantor_{i}_register"] = guarantor?.GuarantorRegister ?? string.Empty;
                context[$"guarantor_{i}_national_id"] = guarantor?.GuarantorNationalId ?? string.Empty;
                context[$"guarantor_{i}_address"] = gAddressPrefix + (guarantor?.GuarantorAddress ?? string.Empty);
                context[$"guarantor_{i}_address_type"] = guarantor?.AddressType ?? "موطن مختار";
            }
            else
            {
                context[$"guarantor_{i}_name"] = string.Empty;
                context[$"guarantor_{i}_father"] = string.Empty;
                context[$"guarantor_{i}_family"] = string.Empty;
                context[$"guarantor_{i}_mother"] = string.Empty;
                context[$"guarantor_{i}_birth"] = string.Empty;
                context[$"guarantor_{i}_register"] = string.Empty;
                context[$"guarantor_{i}_national_id"] = string.Empty;
                context[$"guarantor_{i}_address"] = string.Empty;
                context[$"guarantor_{i}_address_type"] = string.Empty;
            }
        }

        // الضمانات العقارية
        var estates = doc.RealEstates.ToList();
        context["real_estates"] = estates;
        context["property"] = estates.Count > 0 ? estates[0].Property ?? string.Empty : string.Empty;
        context["property_owner"] = estates.Count > 0 ? estates[0].Owner ?? string.Empty : string.Empty;

        // المنفذ عليهم مع عناوينهم (RichText بالاسم العريض)
        context["execution_debtors_and_its_adresses"] =
            BuildDebtorsRichXml(borrowerFull, borrowerAddress, doc.BorrowerAddressType, guarantors);

        // المنفذون عليهم نصاً عادياً (لكل منها سطر) — مستخدم في قالب الحجز العقاري
        context["execution_debtors"] = BuildExecutionDebtorsPlain(borrowerFull, guarantors);
        context["branch"] = doc.BranchName ?? string.Empty;

        // تعديلات خاصة بنوع القالب (مطابقة build_document_context)
        if (templateCode is "001" or "002")
        {
            if (!string.IsNullOrWhiteSpace(borrowerFull))
            {
                context["borrower_address"] = BuildBorrowerAddressRichXml(
                    borrowerFull, addressPrefix + (doc.BorrowerAddress ?? string.Empty));
                context["borrower_name"] = string.Empty;
                context["borrower_father"] = string.Empty;
                context["borrower_family"] = string.Empty;
            }
        }
        else if (templateCode == "003")
        {
            context["contain"] = context["contain_notice"];

            // إخطار تنفيذي لكفيل محدد (recipient = رقم الكفيل)
            if (recipient > 0)
            {
                var guarantor = guarantors.FirstOrDefault(g => g.GuarantorNumber == recipient)
                    ?? throw new ArgumentException($"كفيل غير موجود: {recipient}");
                BuildGuarantorNoticeContext(context, guarantor);
            }
        }
        else if (templateCode == "005")
        {
            var selected = SelectEstates(estates, estateIds);
            BuildPropertySaleContext(context, selected);
        }
        else if (templateCode == "006")
        {
            var selected = SelectEstates(estates, estateIds);
            BuildPropertySalePaperContext(context, selected, borrowerFull, guarantors);
        }
        else if (templateCode == "007")
        {
            var target = ResolveRecipient(recipient, doc, guarantors);
            BuildNoticePaperContext(context, target, recipient, isOrdinary);
        }
        else if (templateCode == "PS")
        {
            var selected = SelectEstates(estates, estateIds);
            BuildPropertySeizureContext(context, selected[0], borrowerFull, guarantors);
        }
        else if (templateCode == "004")
        {
            var seizureDate = (doc.SeizureDate ?? string.Empty).Trim();
            var totalGuarantors = guarantors.Count(g => !string.IsNullOrWhiteSpace(g.GuarantorName));
            var hasGuarantors = totalGuarantors > 0;

            if (!string.IsNullOrEmpty(seizureDate) && hasGuarantors)
            {
                var filePrefix = string.Join(' ', new[]
                {
                    (doc.FileNumber ?? string.Empty).Trim(),
                    (doc.FileType ?? string.Empty).Trim()
                }.Where(p => p.Length > 0));
                context["seizure_date"] = $"{filePrefix}\nتاريخ القرار: {seizureDate}";
            }

            var totalDebtors = 1 + totalGuarantors;
            var szPrefix = string.Join(' ', new[]
            {
                (doc.FileNumber ?? string.Empty).Trim(),
                (doc.FileType ?? string.Empty).Trim()
            }.Where(p => p.Length > 0));
            var szValue = string.IsNullOrEmpty(seizureDate)
                ? string.Empty
                : $"{szPrefix}\nتاريخ القرار: {seizureDate}";
            var courtPrefix = (string)context["court_with_prefix"];

            for (var n = 1; n <= 5; n++)
            {
                if (n <= totalDebtors)
                {
                    context[$"seizure_date{n}"] = szValue;
                    context[$"court_with_prefix{n}"] = courtPrefix;
                }
                else
                {
                    context[$"seizure_date{n}"] = string.Empty;
                    context[$"court_with_prefix{n}"] = string.Empty;
                }
            }
        }

        return context;
    }

    private static string BuildBorrowerAddressRichXml(string borrowerFull, string addressText)
    {
        if (string.IsNullOrWhiteSpace(borrowerFull))
            return addressText;

        var xml = $"<w:r><w:rPr><w:b/><w:rtl/></w:rPr><w:t xml:space=\"preserve\">{XmlEscape(borrowerFull)}</w:t></w:r>";
        if (!string.IsNullOrWhiteSpace(addressText))
            xml += $"<w:r><w:rPr><w:rtl/></w:rPr><w:t xml:space=\"preserve\"> {XmlEscape(addressText)}</w:t></w:r>";
        return xml;
    }

    private static string BuildDebtorsRichXml(
        string borrowerFull,
        string borrowerAddress,
        string? borrowerAddressType,
        List<Guarantor> guarantors)
    {
        var items = new List<(string Name, string Address)>();

        if (!string.IsNullOrWhiteSpace(borrowerFull))
        {
            var address = string.Empty;
            if (!string.IsNullOrWhiteSpace(borrowerAddress))
                address = borrowerAddressType == "موطن مختار"
                    ? $"متخذا موطنا مختارا {borrowerAddress}"
                    : $"عنوانه {borrowerAddress}";
            items.Add((borrowerFull, address));
        }

        for (var i = 1; i <= 5; i++)
        {
            var guarantor = guarantors.FirstOrDefault(g => g.GuarantorNumber == i);
            var name = string.Join(' ', new[]
            {
                guarantor?.GuarantorName, guarantor?.GuarantorFather, guarantor?.GuarantorFamily
            }.Where(v => !string.IsNullOrWhiteSpace(v)));

            if (string.IsNullOrWhiteSpace(name))
                continue;

            var address = string.Empty;
            if (!string.IsNullOrWhiteSpace(guarantor?.GuarantorAddress))
                address = guarantor.AddressType == "موطن مختار"
                    ? $"متخذا موطنا مختارا {guarantor.GuarantorAddress}"
                    : $"عنوانه {guarantor.GuarantorAddress}";
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

    private static List<RealEstate> SelectEstates(List<RealEstate> estates, int[]? estateIds)
    {
        if (estateIds is null || estateIds.Length == 0)
            throw new ArgumentException("يرجى اختيار عقار واحد على الأقل");

        var selected = estates.Where(e => estateIds.Contains(e.Id)).ToList();
        if (selected.Count == 0)
            throw new ArgumentException("العقار المحدد غير موجود");

        return selected;
    }

    // ── إخطار تنفيذي لكفيل محدد (003 + recipient) — مطابق generate_guarantor_notice ──
    private static void BuildGuarantorNoticeContext(Dictionary<string, object> context, Guarantor guarantor)
    {
        var name = guarantor.GuarantorName ?? string.Empty;
        var addressPrefix = guarantor.AddressType == "موطن مختار" ? "موطناً مختاراً " : string.Empty;
        var address = addressPrefix + (guarantor.GuarantorAddress ?? string.Empty);

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
    private static void BuildPropertySaleContext(Dictionary<string, object> context, List<RealEstate> estates)
    {
        var owner = estates[0].Owner ?? string.Empty;
        var (ownerName, ownerFather, ownerFamily) = SplitOwnerName(owner);

        context["property"] = FormatProperties(estates);
        context["property_owner"] = owner;
        context["borrower_name"] = ownerName;
        context["borrower_father"] = ownerFather;
        context["borrower_family"] = ownerFamily;

        if (!string.IsNullOrWhiteSpace((string)context["amount_words"]))
            context["amount_words"] = ((string)context["amount_words"]).Trim() + " مع توابعه القانونية";
    }

    // ── إخطار بيع أموال غير منقولة بالصحف (006) — مطابق generate_006_paper_notice ──
    private static void BuildPropertySalePaperContext(
        Dictionary<string, object> context,
        List<RealEstate> estates,
        string borrowerFull,
        List<Guarantor> guarantors)
    {
        var owner = estates[0].Owner ?? string.Empty;
        var combined = FormatProperties(estates);

        context["property"] = combined;
        context["property_owner"] = owner;
        context["execution_debtor"] = ResolveOwnerFullName(owner, borrowerFull, guarantors);

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
        RealEstate estate,
        string borrowerFull,
        List<Guarantor> guarantors)
    {
        var amountWords = (string)context["amount_words"];
        if (amountWords.StartsWith("الزامك بدفع ", StringComparison.Ordinal))
            amountWords = amountWords["الزامك بدفع ".Length..];

        context["Land_Registry"] = estate.LandRegistry ?? string.Empty;
        context["execution_debtor"] = estate.Owner ?? string.Empty;
        context["property_number"] = estate.PropertyNumber ?? string.Empty;
        context["property_district"] = estate.PropertyDistrict ?? string.Empty;
        context["execution_debtors"] = BuildExecutionDebtorsPlain(borrowerFull, guarantors);
        context["amount_words"] = amountWords;
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

    private static string FormatProperties(List<RealEstate> estates)
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
