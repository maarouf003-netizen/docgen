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
        int heirId = 0,
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
        // الرقم الظاهر في المستندات: رقم أساس السنة الحالية إن وُجد، وإلا رقم الملف الأصلي
        // (القرار: رقم الأساس للسنة الحالية يحل محل رقم الملف في المستندات المولدة).
        var effectiveFileNumber = doc.BaseNumbers
            .Where(b => b.Year == DateTime.Today.Year)
            .Select(b => b.BaseNumber)
            .FirstOrDefault() ?? doc.FileNumber ?? string.Empty;
        context["file_number"] = effectiveFileNumber;
        context["file_type"] = doc.FileType ?? string.Empty;
        context["file_year"] = doc.FileYear ?? string.Empty;
        context["file_number_full"] = string.IsNullOrWhiteSpace(doc.FileYear)
            ? effectiveFileNumber
            : $"{effectiveFileNumber}/{doc.FileYear}";
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
                context["execution_debtor_and_its_adress"] =
                    $"{borrowerFull}\n{AddressPhraseOf(doc.BorrowerAddressType, borrowerAddress, withColon: true)}";
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

        context["borrower_address"] = PrefixedAddress(doc.BorrowerAddressType, doc.BorrowerAddress);
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
        var heirs = doc.Heirs.ToList();
        for (var i = 1; i <= 5; i++)
        {
            var guarantor = guarantors.FirstOrDefault(g => g.GuarantorNumber == i);
            var name = guarantor?.GuarantorName ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(name))
            {
                context[$"guarantor_{i}_name"] = name;
                context[$"guarantor_{i}_father"] = guarantor?.GuarantorFather ?? string.Empty;
                context[$"guarantor_{i}_family"] = guarantor?.GuarantorFamily ?? string.Empty;
                context[$"guarantor_{i}_mother"] = guarantor?.GuarantorMother ?? string.Empty;
                context[$"guarantor_{i}_birth"] = guarantor?.GuarantorBirth ?? string.Empty;
                context[$"guarantor_{i}_register"] = guarantor?.GuarantorRegister ?? string.Empty;
                context[$"guarantor_{i}_national_id"] = guarantor?.GuarantorNationalId ?? string.Empty;
                context[$"guarantor_{i}_address"] = PrefixedAddress(guarantor?.AddressType, guarantor?.GuarantorAddress);
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
        context["property_owner"] = estates.Count > 0 ? JoinOwners(OwnerNames(estates[0])) : string.Empty;

        // المنفذ عليهم مع عناوينهم (RichText بالاسم العريض)
        context["execution_debtors_and_its_adresses"] =
            BuildDebtorsRichXml(borrowerFull, borrowerAddress, doc.BorrowerAddressType, guarantors, heirs);

        // المنفذون عليهم نصاً عادياً (لكل منها سطر) — مستخدم في قالب الحجز العقاري
        context["execution_debtors"] = BuildExecutionDebtorsPlain(borrowerFull, guarantors);
        context["branch"] = doc.BranchName ?? string.Empty;

        // تعديلات خاصة بنوع القالب (مطابقة build_document_context)
        if (templateCode is "001" or "002")
        {
            if (!string.IsNullOrWhiteSpace(borrowerFull))
            {
                var borrowerHeirs = HeirsOf(heirs, null);
                if (borrowerHeirs.Count > 0)
                {
                    var heirsItem = BuildHeirsRichItem(borrowerFull, borrowerHeirs);
                    context["borrower_address"] = BuildRichPairXml(heirsItem.Name, heirsItem.Address);
                }
                else
                {
                    context["borrower_address"] = BuildBorrowerAddressRichXml(
                        borrowerFull, PrefixedAddress(doc.BorrowerAddressType, doc.BorrowerAddress));
                }
                context["borrower_name"] = string.Empty;
                context["borrower_father"] = string.Empty;
                context["borrower_family"] = string.Empty;
            }
        }
        else if (templateCode == "003")
        {
            context["contain"] = context["contain_notice"];

            // إخطار تنفيذي لوريث محدد (heirId = معرف الوريث)
            if (heirId > 0)
            {
                BuildHeirNoticeContext(context, ResolveHeirContext(heirs, heirId, borrowerFull, guarantors));
            }
            // إخطار تنفيذي لكفيل محدد (recipient = رقم الكفيل)
            else if (recipient > 0)
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
            if (heirId > 0)
            {
                var resolved = ResolveHeirContext(heirs, heirId, borrowerFull, guarantors);
                context["execution_debtor_and_its_adress"] =
                    BuildHeirLine(resolved.Heir, resolved.DeceasedFullName, withAddress: true);
            }
        }
        else if (templateCode == "006")
        {
            var selected = SelectEstates(estates, estateIds);
            BuildPropertySalePaperContext(context, selected, borrowerFull, guarantors);
            if (heirId > 0)
            {
                var resolved = ResolveHeirContext(heirs, heirId, borrowerFull, guarantors);
                context["execution_debtor"] =
                    BuildHeirLine(resolved.Heir, resolved.DeceasedFullName, withAddress: false);
            }
        }
        else if (templateCode == "007")
        {
            if (heirId > 0)
                BuildHeirPaperContext(context, ResolveHeirContext(heirs, heirId, borrowerFull, guarantors));
            else
            {
                var target = ResolveRecipient(recipient, doc, guarantors);
                BuildNoticePaperContext(context, target, recipient, isOrdinary);
            }
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
    /// سطر الوريث في الإخطارات: «[الوريث] إضافة لتركة المتوفى ([المتوفى])». مع
    /// (withAddress) تُلحق عبارة العنوان/الوكيل في سطر تالٍ إن وُجدت — وتُهمل في
    /// إخطارات الصحف (006/007) التي تذكر الوريث والعبارة فقط بلا عنوان ولا وكيل.
    /// </summary>
    private static string BuildHeirLine(Heir heir, string deceasedFull, bool withAddress)
    {
        var line = $"{heir.HeirName?.Trim()} إضافة لتركة المتوفى ({deceasedFull})";
        if (!withAddress)
            return line;

        var phrase = HeirAddressPhrase(heir);
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
                var name = (h.HeirName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return string.Empty;
                var phrase = HeirAddressPhrase(h);
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
        context["borrower_name"] = resolved.Heir.HeirName ?? string.Empty;
        context["execution_debtor_and_its_adress"] =
            BuildHeirLine(resolved.Heir, resolved.DeceasedFullName, withAddress: true);
    }

    /// <summary>إخطار تنفيذي بالصحف موجَّه إلى وريث (007 + heirId): بلا عنوان ولا وكيل.</summary>
    private static void BuildHeirPaperContext(
        Dictionary<string, object> context,
        (Heir Heir, string DeceasedFullName) resolved)
    {
        var heirName = resolved.Heir.HeirName ?? string.Empty;
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

        for (var i = 1; i <= 5; i++)
        {
            var guarantor = guarantors.FirstOrDefault(g => g.GuarantorNumber == i);
            var name = string.Join(' ', new[]
            {
                guarantor?.GuarantorName, guarantor?.GuarantorFather, guarantor?.GuarantorFamily
            }.Where(v => !string.IsNullOrWhiteSpace(v)));

            if (string.IsNullOrWhiteSpace(name))
                continue;

            var guarantorHeirs = HeirsOf(heirs, i);
            if (guarantorHeirs.Count > 0)
            {
                items.Add(BuildHeirsRichItem(name, guarantorHeirs));
                continue;
            }

            var address = string.Empty;
            if (!string.IsNullOrWhiteSpace(guarantor?.GuarantorAddress))
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
    /// أسماء ملاك عقار بترتيب الاختيار (من قائمة الملاك الفرعية).
    /// </summary>
    private static List<string> OwnerNames(RealEstate estate) =>
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
    private static void BuildPropertySaleContext(Dictionary<string, object> context, List<RealEstate> estates)
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
        List<RealEstate> estates,
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
        RealEstate estate,
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
