using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentService : IDocumentService
{
    private static void ApplyRequest(Document doc, DocumentUpsertRequest r)
    {
        // صفة الملف تُثبَّت عند الإنشاء ولا تُغيَّر عند التعديل (الموافقة المعتمدة):
        // على التعديل يبقى جانب الملف كما هو مهما أُرسل في الطلب.
        // (ValidateSide تطبَّعت القيمة مسبقًا إلى قيمة صالحة غير فارغة).
        if (doc.Id == 0 || doc.GeneralEntitySide == r.GeneralEntitySide)
            doc.GeneralEntitySide = r.GeneralEntitySide!;

        doc.DocumentType = r.DocumentType;
        doc.BorrowerName = r.BorrowerName;
        doc.BorrowerFather = r.BorrowerFather;
        doc.BorrowerFamily = r.BorrowerFamily;
        doc.BorrowerMother = r.BorrowerMother;
        doc.BorrowerBirth = r.BorrowerBirth;
        doc.BorrowerRegister = r.BorrowerRegister;
        doc.BorrowerNationalId = r.BorrowerNationalId;
        doc.BorrowerAddress = r.BorrowerAddress;
        doc.BorrowerAddressType = r.BorrowerAddressType;

        var rep = DocumentText.NormalizeRepresentativeFields(
            r.BorrowerRepresentativeName, r.BorrowerRepresentativeFather, r.BorrowerRepresentativeFamily,
            r.BorrowerRepresentativeCapacity, r.BorrowerRepresentativeAddressType, r.BorrowerRepresentativeAddress);
        doc.BorrowerRepresentativeName = rep.Name;
        doc.BorrowerRepresentativeFather = rep.Father;
        doc.BorrowerRepresentativeFamily = rep.Family;
        doc.BorrowerRepresentativeCapacity = rep.Capacity;
        doc.BorrowerRepresentativeAddressType = rep.AddressType;
        doc.BorrowerRepresentativeAddress = rep.Address;

        // طبيعة المقترض: الاعتباري يحمل اسم الشخص الاعتباري في BorrowerName وتُصفَّر حقول الهوية
        // الطبيعية والممثل الشرعي والورثة (مفاهيم تخص الشخص الطبيعي)، ويُحتفظ برقم التسجيل ومن يمثله.
        // الطبيعي يُصفِّر الحقول الاعتبارية.
        doc.BorrowerNature = DocumentText.NormalizePartyNature(r.BorrowerNature);
        if (PartyNatureCatalog.IsLegal(doc.BorrowerNature))
        {
            doc.BorrowerFather = null;
            doc.BorrowerFamily = null;
            doc.BorrowerMother = null;
            doc.BorrowerBirth = null;
            doc.BorrowerRegister = null;
            doc.BorrowerNationalId = null;
            doc.BorrowerRegistrationNumber = (r.BorrowerRegistrationNumber ?? string.Empty).Trim();
            doc.BorrowerRepresentedBy = (r.BorrowerRepresentedBy ?? string.Empty).Trim();
            doc.BorrowerRepresentativeName = null;
            doc.BorrowerRepresentativeFather = null;
            doc.BorrowerRepresentativeFamily = null;
            doc.BorrowerRepresentativeCapacity = null;
            doc.BorrowerRepresentativeAddressType = null;
            doc.BorrowerRepresentativeAddress = null;
        }
        else
        {
            doc.BorrowerRegistrationNumber = null;
            doc.BorrowerRepresentedBy = null;
        }

        doc.ContractType = r.ContractType;
        doc.ContractTypeSelector = r.ContractTypeSelector;
        doc.ContractNumber = r.ContractNumber;
        doc.ContractDate = r.ContractDate;
        // الملاحق: الجدول الفرعي هو المصدر. الأعمدة المفردة القديمة تبقى كما هي
        // (قراءة فقط) ولا تُكتب بعد الآن.
        doc.Annexes.Clear();
        foreach (var annex in DocumentAnnexText.Build(r))
            doc.Annexes.Add(annex);
        doc.InclusionText = r.InclusionText;
        doc.InclusionNotes = r.InclusionNotes;
        // المبالغ: الأرقام والعملات من الطلب، والتفقيط مشتقّ خادميًا من الرقم الجديد
        // (`DocumentText.DeriveAmountWords`) — لا يُقبل نص العميل أصلًا.
        doc.AmountWords = DocumentText.DeriveAmountWords(doc.AmountNumeric, r.AmountNumeric ?? 0, r.Currency, doc.AmountWords);
        doc.AmountNumeric = r.AmountNumeric ?? 0;
        doc.Currency = r.Currency;
        doc.Amount2Words = DocumentText.DeriveAmountWords(doc.Amount2Numeric, r.Amount2Numeric ?? 0, r.Currency2, doc.Amount2Words);
        doc.Amount2Numeric = r.Amount2Numeric ?? 0;
        doc.Currency2 = r.Currency2;
        doc.Amount3Words = DocumentText.DeriveAmountWords(doc.Amount3Numeric, r.Amount3Numeric ?? 0, r.Currency3, doc.Amount3Words);
        doc.Amount3Numeric = r.Amount3Numeric ?? 0;
        doc.Currency3 = r.Currency3;
        doc.InclusionAmountWords = DocumentText.DeriveAmountWords(doc.InclusionAmountNumeric, r.InclusionAmountNumeric ?? 0, r.InclusionCurrency, doc.InclusionAmountWords);
        doc.InclusionAmountNumeric = r.InclusionAmountNumeric ?? 0;
        doc.InclusionCurrency = r.InclusionCurrency;
        doc.InclusionAmount2Words = DocumentText.DeriveAmountWords(doc.InclusionAmount2Numeric, r.InclusionAmount2Numeric ?? 0, r.InclusionCurrency2, doc.InclusionAmount2Words);
        doc.InclusionAmount2Numeric = r.InclusionAmount2Numeric ?? 0;
        doc.InclusionCurrency2 = r.InclusionCurrency2;
        doc.InclusionAmount3Words = DocumentText.DeriveAmountWords(doc.InclusionAmount3Numeric, r.InclusionAmount3Numeric ?? 0, r.InclusionCurrency3, doc.InclusionAmount3Words);
        doc.InclusionAmount3Numeric = r.InclusionAmount3Numeric ?? 0;
        doc.InclusionCurrency3 = r.InclusionCurrency3;
        // العقد: الخادم يشتق Court/CourtNorm من الدائرة ويتجاهل نص العميل دائمًا (لا 400) —
        // يُضبط في Create/Update عبر ApplyCircuitDerivation بعد ApplyRequest.
        // التطبيع الرقمي مركزيًا: الثقب 123 مقابل ١٢٣ يُغلق هنا للإنشاء/التعديل.
        doc.FileNumber = DigitNormalizer.NormalizeDigitsOrNull(r.FileNumber);
        doc.FileType = r.FileType;
        doc.FileYear = DigitNormalizer.NormalizeDigitsOrNull(r.FileYear);
        doc.FileIncoming = r.FileIncoming;
        doc.FileIncomingDate = r.FileIncomingDate;
        doc.UnderFilingNumber = r.UnderFilingNumber;
        doc.BranchName = r.BranchName;
        doc.SeizureDate = r.SeizureDate;
        doc.ImmediateActions = r.ImmediateActions;
        doc.Notes = r.Notes;

        // حقلا ورود الملف خاصان بوضع «طالبة تنفيذ» فقط ويُصفَّران بغيرها.
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            doc.FileArrivalNumber = null;
            doc.FileArrivalDate = null;
        }
        else
        {
            var arrivalNumber = (r.FileArrivalNumber ?? string.Empty).Trim();
            var arrivalDate = (r.FileArrivalDate ?? string.Empty).Trim();
            doc.FileArrivalNumber = string.IsNullOrEmpty(arrivalNumber) ? null : arrivalNumber;
            doc.FileArrivalDate = string.IsNullOrEmpty(arrivalDate) ? null : arrivalDate;
        }

        // حقول عائلة وضع «منفذ عليه» (Executed + Deposit): تُطبَّق على ملفات هذه الصفة فقط،
        // وتُصفَّر خارجها. صفة العرض لا تحمل وصفًا إضافيًا (ExecutedDescription) بل تاريخ إيداع.
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            var executedStatus = string.IsNullOrWhiteSpace(r.ExecutedStatus)
                ? ExecutedStatusCatalog.None
                : r.ExecutedStatus.Trim();
            if (!ExecutedStatusCatalog.ValidStatuses.Contains(executedStatus))
                throw new ArgumentException("حالة وضع (متداول/منفذ/مشطوب) غير صالحة");

            // شطبٌ جديد بتاريخ مصرّح يُحفظ كما هو؛ شطبٌ جديد بتاريـخ فارغ يحمل تاريخ الآن؛
            // وإعادة شطب (كان مشطوبًا قبل التعديل) بلا تاريخ صريح تُبقي تاريخها السابق —
            // فلا يُعاد استخدام تاريخ شطب قديمٍ كحدثٍ جديد (يوحّد مع قاعدة Status.cs).
            var wasStruckOff = ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus);
            doc.ExecutedStatus = ExecutedStatusCatalog.IsStored(executedStatus) ? executedStatus : ExecutedStatusCatalog.None;
            if (doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
            {
                var submitted = DocumentValidator.ParseDateTime(r.StruckOffDate, "تاريخ الشطب");
                if (submitted is not null)
                    doc.StruckOffDate = submitted;
                else if (!wasStruckOff)
                    doc.StruckOffDate = DateTime.UtcNow;
            }
            doc.ExecutedDescription = doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                ? (r.ExecutedDescription ?? string.Empty).Trim()
                : null;
            doc.FileReceiptDate = DocumentValidator.ParseDateTime(r.FileReceiptDate, "تاريخ ورود الاخطار");
            doc.FileReceiptNumber = (r.FileReceiptNumber ?? string.Empty).Trim();
            doc.ExecutedRequiredAmount = r.ExecutedRequiredAmount;
            doc.ExecutedRequiredCurrency = r.ExecutedRequiredCurrency;
            doc.ExecutedRequiredAmount2 = r.ExecutedRequiredAmount2;
            doc.ExecutedRequiredCurrency2 = r.ExecutedRequiredCurrency2;
            doc.ExecutedRequiredAmount3 = r.ExecutedRequiredAmount3;
            doc.ExecutedRequiredCurrency3 = r.ExecutedRequiredCurrency3;
            doc.ExecutedPaidAmount = r.ExecutedPaidAmount;
            doc.ExecutedPaidCurrency = r.ExecutedPaidCurrency;
            doc.ExecutedPaidAmount2 = r.ExecutedPaidAmount2;
            doc.ExecutedPaidCurrency2 = r.ExecutedPaidCurrency2;
            doc.ExecutedPaidAmount3 = r.ExecutedPaidAmount3;
            doc.ExecutedPaidCurrency3 = r.ExecutedPaidCurrency3;
            doc.ExecutedDepositDate = doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
                ? DocumentValidator.ParseDateTime(r.ExecutedDepositDate, "تاريخ ايداعه حساب الجهة العامة")
                : null;
            doc.ExecutedExecutionDate = doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                ? DocumentValidator.ParseDateTime(r.ExecutedExecutionDate, "تاريخ التنفيذ")
                : null;
        }
        else
        {
            doc.ExecutedStatus = ExecutedStatusCatalog.None;
            doc.ExecutedDescription = null;
            doc.FileReceiptDate = null;
            doc.FileReceiptNumber = null;
            doc.ExecutedRequiredAmount = null;
            doc.ExecutedRequiredCurrency = null;
            doc.ExecutedRequiredAmount2 = null;
            doc.ExecutedRequiredCurrency2 = null;
            doc.ExecutedRequiredAmount3 = null;
            doc.ExecutedRequiredCurrency3 = null;
            doc.ExecutedPaidAmount = null;
            doc.ExecutedPaidCurrency = null;
            doc.ExecutedPaidAmount2 = null;
            doc.ExecutedPaidCurrency2 = null;
            doc.ExecutedPaidAmount3 = null;
            doc.ExecutedPaidCurrency3 = null;
            doc.ExecutedDepositDate = null;
            doc.ExecutedExecutionDate = null;
            // لا يُمس StruckOffDate هنا: يديره DocumentStatusService، ويبقى تاريخًا
            // محفوظًا بعد إعادة الملف المشطوب إلى المتداول لعرضه في السجل.
        }

        // تفريغ صريح لورثة المنفذ عليهم قبل إعادة بنائهم: كان الاعتماد على Cascade
        // يترك صفوفًا قديمة/مكررة في الذاكرة وسجل التعديلات عند استبدال المورث.
        doc.ExecutedHeirs.Clear();
        doc.ExecutionApplicants.Clear();
        foreach (var a in NormalizeExecutionApplicants(r.ExecutionApplicants))
        {
            doc.ExecutionApplicants.Add(a);
            // ربط الورثة بالملف مباشرة (DocumentId) وبمورثهم (ExecutionApplicantId):
            // EF يرتب كل مفتاح أجنبي عبر مجموعة المورث ومجموعة الملف معًا.
            foreach (var heir in a.Heirs)
                doc.ExecutedHeirs.Add(heir);
        }

        doc.ExecutedPublicEntities.Clear();
        foreach (var e in NormalizeExecutedPublicEntities(r.ExecutedPublicEntities))
            doc.ExecutedPublicEntities.Add(e);

        // قائمة الجهات طالبة التنفيذ: تخص وضع «طالبة تنفيذ» فقط وتُصفَّر بغيره.
        // لا تُستخدم `!IsExecutedLike` هنا: أي صفة مستقبلية ليست منفذًا عليها كانت
        // ستُحوِّل نص `Applicant` القديم إلى صف جهة عامة وهمي بلا RegistryId.
        doc.ApplicantPublicEntities.Clear();
        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Applicant)
        {
            var applicantList = r.ApplicantPublicEntities;
            // توافق مع الطلبات القديمة: نص «طالب التنفيذ» المرسل بلا قائمة يُعامَل كجهة واحدة.
            if ((applicantList is null || applicantList.Count == 0) && !string.IsNullOrWhiteSpace(r.Applicant))
                applicantList = new List<ApplicantPublicEntityDto> { new(null, r.Applicant, null) };
            foreach (var a in DocumentText.NormalizeApplicantPublicEntities(applicantList))
                doc.ApplicantPublicEntities.Add(a);
        }
        else
        {
            // النص القديم `Applicant` لا يخصّ أوضاع «منفذ عليه/عرض وايداع»؛ تصفيره يمنع
            // بقاء اسم طالب تنفيذ قديم في حقل عرض يُقرأ احتياطيًا في الواجهة.
            doc.Applicant = null;
        }

        doc.ExecutedNaturalPersons.Clear();
        foreach (var p in NormalizeExecutedNaturalPersons(r.ExecutedNaturalPersons))
        {
            doc.ExecutedNaturalPersons.Add(p);
            foreach (var heir in p.Heirs)
                doc.ExecutedHeirs.Add(heir);
        }

        doc.Guarantors.Clear();
        foreach (var g in r.Guarantors.OrderBy(g => g.GuarantorNumber))
            doc.Guarantors.Add(DocumentText.BuildGuarantor(g));

        // الورثة: صفوف بلا اسم ثلاثي تُتجاهل، ونوع العنوان غير الصالح يُعيَّر إلى «عنوان».
        // لا ورثة لشخص اعتباري (ورثة تخص الشخص الطبيعي المتوفى فقط).
        doc.Heirs.Clear();
        if (!PartyNatureCatalog.IsLegal(doc.BorrowerNature))
            foreach (var h in DocumentText.NormalizeHeirs(r.BorrowerHeirs, null))
                doc.Heirs.Add(h);
        foreach (var g in r.Guarantors)
            if (!PartyNatureCatalog.IsLegal(DocumentText.NormalizePartyNature(g.Nature)))
                foreach (var h in DocumentText.NormalizeHeirs(g.Heirs, g.GuarantorNumber))
                    doc.Heirs.Add(h);

        doc.Assets.Clear();
        foreach (var re in r.Assets)
            doc.Assets.Add(DocumentText.BuildAsset(re));
    }

    /// <summary>
    /// تطبيع طلبات التنفيذ: يُتجاهل الطلب بلا اسم ثلاثي، ويُقيَّد نوع التمثيل بالقيم المسموح بها
    /// («أصالة»/«إضافة لتركة»/«أصالة وإضافة») مع معاملة أي قيمة أخرى أو فارغة كـ«أصالة»، ويُقصّ
    /// الاسم الثلاثي للمورث إن لم يُحدَّد مع «إضافة لتركة» أو «أصالة وإضافة». وترتبط ورثة كل مورث
    /// بمجموعته مباشرة، ويُطبَّع الممثل الشرعي (إن وُجد بغير اسم ثلاثي فارغ) حقولَه فيُصفَّر
    /// عند الغياب.
    /// </summary>
    private static List<ExecutionApplicant> NormalizeExecutionApplicants(IEnumerable<ExecutionApplicantDto>? applicants)
    {
        var result = new List<ExecutionApplicant>();
        if (applicants is null)
            return result;

        foreach (var a in applicants)
        {
            var name = (a.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var nature = DocumentText.NormalizePartyNature(a.Nature);
            var isLegal = PartyNatureCatalog.IsLegal(nature);

            // الشخص الاعتباري بلا تمثيل بالتركة ولا ممثل شرعي: تُصفَّر حقول الهوية الطبيعية
            // ويُحتفظ برقم التسجيل ومن يمثلها ونوع العنوان وعنوانه.
            var representationType = isLegal
                ? "أصالة"
                : NormalizeApplicantRepresentation(a.RepresentationType);
            var hasEstate = !isLegal && representationType is "إضافة لتركة" or "أصالة وإضافة";
            var hasRep = !isLegal && !DocumentText.IsEmptyRepresentative(a.RepresentativeName, a.RepresentativeFather, a.RepresentativeFamily);

            var applicant = new ExecutionApplicant
            {
                Name = name,
                ApplicantNature = nature,
                ApplicantRegistrationNumber = isLegal ? (a.RegistrationNumber ?? string.Empty).Trim() : null,
                ApplicantRepresentedBy = isLegal ? (a.RepresentedBy ?? string.Empty).Trim() : null,
                ApplicantAddressType = isLegal ? (a.AddressType ?? string.Empty).Trim() : null,
                ApplicantAddress = isLegal ? (a.Address ?? string.Empty).Trim() : null,
                Father = isLegal ? null : (a.Father ?? string.Empty).Trim(),
                Family = isLegal ? null : (a.Family ?? string.Empty).Trim(),
                // الممثل الشرعي المهيكل هو المصدر حين يوجد؛ لا يُبقى «الوكيل القانوني» النصي معه
                // (الواجهة تصفّره، والخلفية تصفّره دفاعًا حتى لا يتكرر الممثل في النصوص).
                LegalRepresentative = isLegal || hasRep ? null : (a.LegalRepresentative ?? string.Empty).Trim(),
                RepresentationType = representationType,
                DeceasedName = hasEstate ? (a.DeceasedName ?? string.Empty).Trim() : null,
                DeceasedFather = hasEstate ? (a.DeceasedFather ?? string.Empty).Trim() : null,
                DeceasedFamily = hasEstate ? (a.DeceasedFamily ?? string.Empty).Trim() : null,
                RepresentativeName = hasRep ? (a.RepresentativeName ?? string.Empty).Trim() : null,
                RepresentativeFather = hasRep ? (a.RepresentativeFather ?? string.Empty).Trim() : null,
                RepresentativeFamily = hasRep ? (a.RepresentativeFamily ?? string.Empty).Trim() : null,
                RepresentativeCapacity = hasRep ? DocumentText.NormalizeRepresentativeCapacity(a.RepresentativeCapacity) : null,
                RepresentativeLegalRepresentative = hasRep ? (a.RepresentativeLegalRepresentative ?? string.Empty).Trim() : null,
                // الربط السجلي لطالب التنفيذ/العرض الاعتباري أُلغي بقرار المالك:
                // الاسم حر دائمًا، ويبقى عمود RegistryId التاريخي غير مستخدم.
                RegistryId = null,
            };
            if (!isLegal)
            {
                foreach (var heir in DocumentText.NormalizeExecutedHeirs(a.Heirs))
                    applicant.Heirs.Add(heir);
            }
            result.Add(applicant);
        }

        return result;
    }

    /// <summary>
    /// تطبيع المنفذ عليهم الاعتباريين (جهة عامة أو شخص اعتباري): يُتجاهل ما بلا اسم، ويُقصّ
    /// اسمه وفرعه. عند الطبيعة (legal) تُعبَّأ حقول الشخص الاعتباري (رقم التسجيل/من يمثلها/العنوان)
    /// ويُصفَّر فرع الجهة العامة؛ وعند (public) تُصفَّر حقول الشخص الاعتباري.
    /// </summary>
    private static List<ExecutedPublicEntity> NormalizeExecutedPublicEntities(IEnumerable<ExecutedPublicEntityDto>? entities)
    {
        var result = new List<ExecutedPublicEntity>();
        if (entities is null)
            return result;

        foreach (var e in entities)
        {
            var name = (e.EntityName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var nature = NormalizeEntityNature(e.Nature);
            var isLegal = PartyNatureCatalog.IsLegal(nature);
            result.Add(new ExecutedPublicEntity
            {
                EntityName = name,
                EntityBranch = isLegal ? null : (e.EntityBranch ?? string.Empty).Trim(),
                Governorate = (e.Governorate ?? string.Empty).Trim(),
                // ربط السجل المرجعي خاص بجهات الدولة العامة لا بالأشخاص الاعتباريين.
                RegistryId = isLegal ? null : e.RegistryId,
                EntityNature = nature,
                RegistrationNumber = isLegal ? (e.RegistrationNumber ?? string.Empty).Trim() : null,
                RepresentedBy = isLegal ? (e.RepresentedBy ?? string.Empty).Trim() : null,
                AddressType = isLegal ? (e.AddressType ?? string.Empty).Trim() : null,
                Address = isLegal ? (e.Address ?? string.Empty).Trim() : null,
            });
        }

        return result;
    }


    /// <summary>
    /// تطبيع الأشخاص الطبيعيين المنفذ عليهم: يُتجاهل ما بلا اسم ثلاثي، ويُقيَّد نوع العنوان
    /// («عنوان»/«وكيل») مع معاملة أي قيمة أخرى كـ«عنوان»، ونوع التمثيل («أصالة»/«إضافة لتركة»/
    /// «أصالة وإضافة») مع معاملة أي قيمة أخرى كـ«أصالة». وترتبط ورثة كل مورث بمجموعته مباشرة،
    /// ويُطبَّع الممثل الشرعي (إن وُجد) حقولَه فيُصفَّر عند الغياب.
    /// </summary>
    private static List<ExecutedNaturalPerson> NormalizeExecutedNaturalPersons(IEnumerable<ExecutedNaturalPersonDto>? persons)
    {
        var result = new List<ExecutedNaturalPerson>();
        if (persons is null)
            return result;

        foreach (var p in persons)
        {
            var name = (p.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var addressType = (p.AddressType ?? string.Empty).Trim();
            if (addressType != "وكيل")
                addressType = "عنوان";

            var representationType = (p.RepresentationType ?? string.Empty).Trim();
            if (representationType != "إضافة لتركة" && representationType != "أصالة وإضافة")
                representationType = "أصالة";

            var hasEstate = representationType is "إضافة لتركة" or "أصالة وإضافة";
            var rep = DocumentText.NormalizeRepresentativeFields(
                p.RepresentativeName, p.RepresentativeFather, p.RepresentativeFamily,
                p.RepresentativeCapacity, p.RepresentativeAddressType, p.RepresentativeAddress);

            var person = new ExecutedNaturalPerson
            {
                Name = name,
                Father = (p.Father ?? string.Empty).Trim(),
                Family = (p.Family ?? string.Empty).Trim(),
                AddressType = addressType,
                AddressOrRepresentative = (p.AddressOrRepresentative ?? string.Empty).Trim(),
                RepresentationType = representationType,
                DeceasedName = hasEstate ? (p.DeceasedName ?? string.Empty).Trim() : null,
                DeceasedFather = hasEstate ? (p.DeceasedFather ?? string.Empty).Trim() : null,
                DeceasedFamily = hasEstate ? (p.DeceasedFamily ?? string.Empty).Trim() : null,
                RepresentativeName = rep.Name,
                RepresentativeFather = rep.Father,
                RepresentativeFamily = rep.Family,
                RepresentativeCapacity = rep.Capacity,
                RepresentativeAddressType = rep.AddressType,
                RepresentativeAddress = rep.Address,
            };
            foreach (var heir in DocumentText.NormalizeExecutedHeirs(p.Heirs))
                person.Heirs.Add(heir);
            result.Add(person);
        }

        return result;
    }

    /// <summary>
    /// طبيعة المنفذ عليه الاعتباري في وضع «منفذ عليه»: جهة عامة (public) أو شخص اعتباري (legal)؛
    /// أي قيمة أخرى أو فارغة تُعيَّر إلى «جهة عامة».
    /// </summary>
    private static string NormalizeEntityNature(string? nature)
    {
        var value = (nature ?? string.Empty).Trim();
        return PartyNatureCatalog.ValidEntityNatures.Contains(value) ? value : PartyNatureCatalog.PublicEntity;
    }

    /// <summary>
    /// نوع تمثيل طالب التنفيذ المقبول: أصالة / إضافة لتركة / أصالة وإضافة؛ أي قيمة أخرى أو فارغة
    /// تُعيَّر إلى «أصالة».
    /// </summary>
    private static string NormalizeApplicantRepresentation(string? representationType)
    {
        var value = (representationType ?? string.Empty).Trim();
        return value is "إضافة لتركة" or "أصالة وإضافة" ? value : "أصالة";
    }

    private void ApplyRegistrationDate(Document doc, string? value)
    {
        var date = value?.Trim();
        if (string.IsNullOrWhiteSpace(date))
        {
            if (doc.RegistrationDate is not null)
            {
                _registrationDates.Remove(doc.RegistrationDate);
                doc.RegistrationDate = null;
            }
            return;
        }

        if (doc.RegistrationDate is null)
            doc.RegistrationDate = new DocumentRegistrationDate { Date = date };
        else
            doc.RegistrationDate.Date = date;

        doc.RegistrationDate.DateParsed = ActionDateParser.TryParse(date);
    }
}
