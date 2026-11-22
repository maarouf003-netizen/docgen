using System.Text;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public partial class DocumentContextBuilder : IDocumentContextBuilder
{
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
        // مفاتيح الشخص الاعتباري (تُستعمل عند تحديث القوالب لاحقًا؛ لا تؤثر على القوالب الحالية).
        context["borrower_nature"] = doc.BorrowerNature ?? PartyNatureCatalog.Natural;
        context["borrower_registration_number"] = doc.BorrowerRegistrationNumber ?? string.Empty;
        context["borrower_represented_by"] = doc.BorrowerRepresentedBy ?? string.Empty;
        context["contract_type"] = contractType;
        context["contract_number"] = doc.ContractNumber ?? string.Empty;
        context["contract_date"] = doc.ContractDate ?? string.Empty;
        // مفاتيح ملحق العقد (للعقد المصرفي؛ اختيارية وتظهر في القالب إذا وُضعت رموزها).
        // الجدول الفرعي هو المصدر، مع إبقاء المفاتيح المفردة كأول ملحق للتوافق.
        var annexes = doc.Annexes.OrderBy(a => a.Sequence).ToList();
        if (annexes.Count == 0
            && (!string.IsNullOrWhiteSpace(doc.AnnexType)
                || !string.IsNullOrWhiteSpace(doc.AnnexNumber)
                || !string.IsNullOrWhiteSpace(doc.AnnexDate)))
        {
            annexes.Add(new DocGenerator.Domain.Entities.DocumentAnnex
            {
                Sequence = 1,
                AnnexType = doc.AnnexType,
                AnnexNumber = doc.AnnexNumber,
                AnnexDate = doc.AnnexDate,
            });
        }
        context["annexes_count"] = annexes.Count;
        for (var i = 0; i < DocGenerator.Domain.Enums.AnnexLimitsCatalog.MaxAnnexes; i++)
        {
            var annex = i < annexes.Count ? annexes[i] : null;
            context[$"annex{i + 1}_type"] = annex?.AnnexType ?? string.Empty;
            context[$"annex{i + 1}_number"] = annex?.AnnexNumber ?? string.Empty;
            context[$"annex{i + 1}_date"] = annex?.AnnexDate ?? string.Empty;
        }
        context["annex_type"] = annexes.ElementAtOrDefault(0)?.AnnexType ?? string.Empty;
        context["annex_number"] = annexes.ElementAtOrDefault(0)?.AnnexNumber ?? string.Empty;
        context["annex_date"] = annexes.ElementAtOrDefault(0)?.AnnexDate ?? string.Empty;
        context["amount_numeric"] = doc.AmountNumeric;
        context["amount_words"] = doc.AmountWords ?? string.Empty;
        context["current_date"] = ServerClock.TodayString(_clock, _timeZone, "dd/MM/yyyy");
        context["current_date_arabic"] = ToArabicIndicDigits(ServerClock.TodayString(_clock, _timeZone, "dd/MM/yyyy"));
        context["currency"] = doc.Currency ?? "ليرة سورية";
        context["contract_type_selector"] = contractTypeSelector;
        // الرقم الظاهر في المستندات: آخر رقم أساس ≤ سنة اليوم (إن وُجد)، وإلا رقم الملف الأصلي.
        // سنة الرقم المرافقة له من السجل نفسه — لا سنة القيد الأصلية: القوالب تعرض
        // (file_number + file_year) معًا فيجب أن ينتميا للسجل نفسه (المبدأ 3).
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        var effectiveFileNumber = EffectiveFileIdentity.Number(doc, currentYear) ?? string.Empty;
        var effectiveFileYear = EffectiveFileIdentity.Year(doc, currentYear) ?? string.Empty;
        context["file_number"] = effectiveFileNumber;
        context["file_type"] = doc.FileType ?? string.Empty;
        context["file_year"] = effectiveFileYear;
        context["file_number_full"] = string.IsNullOrWhiteSpace(effectiveFileYear)
            ? effectiveFileNumber
            : $"{effectiveFileNumber}/{effectiveFileYear}";
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

        // الكفلاء (حتى سقف PartyLimitsCatalog — تُولَّد خانات مرقمة لكل رقم محتمل،
        // والفارغ يُصفَّر ليبقى القالب الصامت متوافقًا؛ قالب 004 يحوي مقاطع حتى السقف).
        var guarantors = doc.Guarantors.OrderBy(g => g.GuarantorNumber).ToList();
        var heirs = doc.Heirs.ToList();
        for (var i = 1; i <= PartyLimitsCatalog.MaxGuarantors; i++)
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
                context[$"guarantor_{i}_nature"] = guarantor?.GuarantorNature ?? string.Empty;
                context[$"guarantor_{i}_registration_number"] = guarantor?.GuarantorRegistrationNumber ?? string.Empty;
                context[$"guarantor_{i}_represented_by"] = guarantor?.GuarantorRepresentedBy ?? string.Empty;
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
                context[$"guarantor_{i}_nature"] = string.Empty;
                context[$"guarantor_{i}_registration_number"] = string.Empty;
                context[$"guarantor_{i}_represented_by"] = string.Empty;
            }
        }

        // الضمانات العقارية (توليد العقار يقتصر على نوع «عقار»؛ الأنواع الأخرى تُضاف لاحقًا)
        var estates = doc.Assets.Where(a => a.AssetKind == AssetKindCatalog.RealEstate).ToList();
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
                    effectiveFileNumber,
                    (doc.FileType ?? string.Empty).Trim()
                }.Where(p => p.Length > 0));
                context["seizure_date"] = $"{filePrefix}\nتاريخ القرار: {seizureDate}";
            }

            var totalDebtors = 1 + totalGuarantors;
            var szPrefix = string.Join(' ', new[]
            {
                effectiveFileNumber,
                (doc.FileType ?? string.Empty).Trim()
            }.Where(p => p.Length > 0));
            var szValue = string.IsNullOrEmpty(seizureDate)
                ? string.Empty
                : $"{szPrefix}\nتاريخ القرار: {seizureDate}";
            var courtPrefix = (string)context["court_with_prefix"];

            // مقطع قرار/دائرة لكل مدين (المقترض + الكفلاء حتى سقف PartyLimitsCatalog).
            // رقم الكفيل هوية مستقرة فقد يحمل فراغات (حذف أوسط مصرفي لا يُرقِّم من جديد):
            // المقطع n يُملأ برقم الكفيل n-1 نفسه لا بالموضع التسلسلي، وإلا ظهر سطر
            // قرار/دائرة يتيم تحت كتلة الاسم الفارغة في قالب 004.
            var namedNumbers = guarantors
                .Where(g => !string.IsNullOrWhiteSpace(g.GuarantorName))
                .Select(g => g.GuarantorNumber)
                .ToHashSet();
            for (var n = 1; n <= PartyLimitsCatalog.MaxTotalDebtors; n++)
            {
                var filled = n == 1 ? n <= totalDebtors : namedNumbers.Contains(n - 1);
                if (filled)
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
}
