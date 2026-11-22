using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// «الملف المناب مرآةً للمنيب»: مزامنة نسخ من المنيب إلى المناب عند كل تعديل جوهري
/// (ref: docs/delegation-mirror-plan.md §3)، مع حارس تعديل المناب في التعديل
/// (§3-ج) وتنبيهين متعاكسين مدموجين (§3-ب). نسخ الحقول قيمي بلا سكيما؛ الاستثناء الوحيد
/// سجل الإضافات المحلية على صف الإنابة (`LocalAddition*` في `DocumentDelegation`).
/// (مدموج من `DocumentService` — المرحلة 3، نقل ميكانيكي بلا سلوك.)
/// ثابت ملكية المعاملة: المزامنة داخل معاملة المستدعي، وإطلاق التنبيهات بعد تثبيتها —
/// تُحفَظ المواضع حرفيًا (أي تبديل تغيير سلوكي). بلا `_tx` إطلاقًا في هذا الملف.
/// </summary>
public enum DelegationStatusAlertKind
{
    /// <summary>الرسالة العامة القديمة («تغيّرت حالة الملف المنيب») — مسار فك الشطب فقط.</summary>
    Generic,

    /// <summary>T2: المنيب تريث — المناب يوقف إجراءات الإنابة.</summary>
    Deferred,

    /// <summary>T3: عودة المنيب إلى المتداول (تراجع من تريث) — متابعة السير.</summary>
    Returned,

    /// <summary>T4: المنيب منفذ بالتسوية — المناب مسترد يوقف إجراءاته ويعود لمرجعه.</summary>
    Settled,

    /// <summary>T5: المنيب منفذ جبريًا كاملًا — المناب مسترد بتحصيل المبلغ جبريًا.</summary>
    FullyForcibly,
}

public sealed partial class DocumentDelegationService
{
    /// <summary>نوع تنبيه حالة المنيب بعد تغيير حالته عبر UpdateStatusAsync: تريث/تسوية/اكتمال جبري
    /// فحسب؛ «جزئيا» (N1) و«مشطوب» (N7) وسواها كبت بلا تنبيه.</summary>
    public static DelegationStatusAlertKind? DelegationStatusAlertKindFor(Document doc) =>
        doc.ExecStatus switch
        {
            ExecutionStatusCatalog.Deferred => DelegationStatusAlertKind.Deferred,
            ExecutionStatusCatalog.ExecutedBySettlement => DelegationStatusAlertKind.Settled,
            ExecutionStatusCatalog.ExecutedForcibly when doc.ExecSubStatus == ExecutionStatusCatalog.SubFullyExecuted
                => DelegationStatusAlertKind.FullyForcibly,
            _ => null,
        };

    /// <summary>مجمّع خاص لفحص تعديل المناب — لا يعبر أي حدّ عام (يُبنى منه `TargetEditReport`).</summary>
    private sealed class TargetEditResult
    {
        public List<AddedHeirEntry> AddedHeirs { get; } = new();
        public List<string> NewRepresentativeLabels { get; } = new();
    }

    private static string HeirDisplayName(Heir h) => string.Join(' ',
        new[] { h.HeirName, h.HeirFather, h.HeirFamily }.Where(v => !string.IsNullOrWhiteSpace(v)));

    // ── الحراسة: تعديل الملف المناب ────────────────────────────────────────────

    /// <summary>
    /// حارس تعديل المناب (دائم لكل مستندٍ ذي SourceDelegationId — حتى بعد الإتمام):
    /// يرفض أي فرق قيمي في الحقول المقفولة (الكتب/السند/المقترض/الجهات/الكفلاء/الأصول)،
    /// مع سماح الإضافات المحلية الوحيدة (وريث جديد، ممثل شرعي يُعبَّأ عند فراغه)،
    /// وتجاهل صامت لـ ImmediateActions (الحفظ الفارغ لا يمس السابق أصلًا).
    /// يُعاد الورثة الجدد تقريرًا عامًا (`TargetEditReport`) لتوليد تنبيه المنيب بعد نجاح الحفظ —
    /// لا يعبر أي نوع خاص أي حدّ عام.
    /// </summary>
    public TargetEditReport ValidateTargetEdit(Document doc, DocumentUpsertRequest r)
    {
        var result = new TargetEditResult();
        if (doc.SourceDelegationId is null)
            return new TargetEditReport(result.AddedHeirs, result.NewRepresentativeLabels);

        var errors = new List<string>();

        // الكتب الخمسة + تاريخ إلقاء الحجز المستندي (المقفولة والمُزامَنة من المنيب).
        RequireSame(errors, "رقم ورود الملف", r.FileArrivalNumber, doc.FileArrivalNumber);
        RequireSame(errors, "تاريخ ورود الملف", r.FileArrivalDate, doc.FileArrivalDate);
        RequireSame(errors, "رقم كتاب الجهة العامة", r.FileIncoming, doc.FileIncoming);
        RequireSame(errors, "تاريخ كتاب الجهة العامة", r.FileIncomingDate, doc.FileIncomingDate);
        RequireSame(errors, "رقم تحت رفع", r.UnderFilingNumber, doc.UnderFilingNumber);
        // حقلّا «ورود الإخطار التنفيذي» (FileReceiptNumber/FileReceiptDate) خاصان بوضع «منفذ عليه»
        // ويُصفَّران على طالبة تنفيذ عند التخزين (DocumentService.Apply.cs) — خارج عقد المرآة
        // (B6: لا يُفحص تكافؤهما بين النسخ والحارس ولا يُقفلان في الواجهة).
        RequireSame(errors, "تاريخ القاء الحجز", r.SeizureDate, doc.SeizureDate);

        // السند التنفيذي كاملًا.
        RequireSame(errors, "نوع العقد", r.ContractType, doc.ContractType);
        RequireSame(errors, "نوع العقد (المفصل)", r.ContractTypeSelector, doc.ContractTypeSelector);
        RequireSame(errors, "رقم العقد", r.ContractNumber, doc.ContractNumber);
        RequireSame(errors, "تاريخ العقد", r.ContractDate, doc.ContractDate);
        if (!DocumentAnnexText.ListsEqual(
                DocumentAnnexText.Build(r),
                doc.Annexes.OrderBy(a => a.Sequence).ToList()))
        {
            errors.Add("ملاحق السند التنفيذي مقفلة على الملف المناب");
        }
        RequireSame(errors, "نص الإدراج", r.InclusionText, doc.InclusionText);
        RequireSame(errors, "ملاحظات السند (القرارات الأخرى)", r.InclusionNotes, doc.InclusionNotes);
        RequireSameNumeric(errors, "المبلغ (الأول)", r.AmountNumeric, doc.AmountNumeric);
        // التفقيط («... كتابة») ليس حقلًا مقفولًا يُقارن: هو مشتقّ خادميًا من الرقم والعملة
        // (`DocumentText.DeriveAmountWords`) ولا يُرسَل من العميل أصلًا، فمقارنته كانت
        // ترفض كل تعديلٍ مشروع على منابٍ ذي مبالغ (نص الطلب `null` مقابل نص المخزَّن المولَّد).
        // قفل المبلغ يبقى محكمًا بمقارنة الرقم (`RequireSameNumeric`) والعملة أدناه.
        RequireSame(errors, "عملة المبلغ الأول", r.Currency, doc.Currency);
        RequireSameNumeric(errors, "المبلغ (الثاني)", r.Amount2Numeric, doc.Amount2Numeric);
        RequireSame(errors, "عملة المبلغ الثاني", r.Currency2, doc.Currency2);
        RequireSameNumeric(errors, "المبلغ (الثالث)", r.Amount3Numeric, doc.Amount3Numeric);
        RequireSame(errors, "عملة المبلغ الثالث", r.Currency3, doc.Currency3);
        RequireSameNumeric(errors, "المبلغ المدرج (الأول)", r.InclusionAmountNumeric, doc.InclusionAmountNumeric);
        RequireSame(errors, "عملة المبلغ المدرج", r.InclusionCurrency, doc.InclusionCurrency);
        RequireSameNumeric(errors, "المبلغ المدرج (الثاني)", r.InclusionAmount2Numeric, doc.InclusionAmount2Numeric);
        RequireSame(errors, "عملة المبلغ المدرج الثاني", r.InclusionCurrency2, doc.InclusionCurrency2);
        RequireSameNumeric(errors, "المبلغ المدرج (الثالث)", r.InclusionAmount3Numeric, doc.InclusionAmount3Numeric);
        RequireSame(errors, "عملة المبلغ المدرج الثالث", r.InclusionCurrency3, doc.InclusionCurrency3);
        // الدائرة خارج عقد المرآة: حقيقة خاصة بالمناب (الدائرة المنابة المسجَّل فيها)،
        // تُضبط عند الاعتماد وقابلة للتعديل كحقل أصيل — لا يُفحص تكافؤها مع المنيب.
        RequireSame(errors, "المدعي", r.Applicant, doc.Applicant);

        // نواة المقترض (عدا حقول الممثل — مقفلة ومُزامَنة، والممثل إضافة محلية).
        RequireSame(errors, "اسم المقترض", r.BorrowerName, doc.BorrowerName);
        RequireSame(errors, "اسم والد المقترض", r.BorrowerFather, doc.BorrowerFather);
        RequireSame(errors, "اسم عائلة المقترض", r.BorrowerFamily, doc.BorrowerFamily);
        RequireSame(errors, "اسم أم المقترض", r.BorrowerMother, doc.BorrowerMother);
        RequireSame(errors, "تاريخ ولادة المقترض", r.BorrowerBirth, doc.BorrowerBirth);
        RequireSame(errors, "سجل المقترض", r.BorrowerRegister, doc.BorrowerRegister);
        RequireSame(errors, "الرقم الوطني للمقترض", r.BorrowerNationalId, doc.BorrowerNationalId);
        RequireSame(errors, "طبيعة المقترض", r.BorrowerNature, doc.BorrowerNature);
        RequireSame(errors, "رقم تسجيل المقترض", r.BorrowerRegistrationNumber, doc.BorrowerRegistrationNumber);
        RequireSame(errors, "من يمثل المقترض", r.BorrowerRepresentedBy, doc.BorrowerRepresentedBy);

        // الجهات العامة طالبة التنفيذ: استبدال كامل من المنيب — أي تغيّر مرفوض. تُطابَق القائمة
        // الافتراضية كما يخزّنها ApplyRequest على الجانبين معًا (تكرار التراجع إلى نص «طالب
        // التنفيذ» كجهة واحدة عند غياب القائمة — DocumentService.Apply.cs) فلا يُرفض الحفظُ
        // المتطابق بفرقٍ وهمي من التراجع النصي نفسه، ويُعيَّن الفرقُ الحقيقي فقط (T4/B6).
        var requestedEntities = EffectiveApplicantEntities(
            DocumentText.NormalizeApplicantPublicEntities(r.ApplicantPublicEntities), r.Applicant);
        var storedEntities = EffectiveApplicantEntities(doc.ApplicantPublicEntities, doc.Applicant);
        if (!ApplicantEntitySetsEqual(requestedEntities, storedEntities))
            errors.Add("الجهات العامة طالبة التنفيذ مقفولة على الملف المناب");

        // ورثة المقترض والممثل الشرعي له: إضافة الناقص أو تعبئة الفراغ فقط — لا حذف ولا تعديل
        // لقائم، ولا ورثة ولا ممثل على مقترض اعتباري (ApplyRequest يُسقطها بصمت فيُرفض صراحةً).
        var borrowerNature = DocumentText.NormalizePartyNature(r.BorrowerNature);
        var isLegalBorrower = PartyNatureCatalog.IsLegal(borrowerNature);
        var requestedBorrowerHeirs = DocumentText.NormalizeHeirs(r.BorrowerHeirs, null);
        var borrowerHasRepInRequest = !DocumentText.IsEmptyRepresentative(
            r.BorrowerRepresentativeName, r.BorrowerRepresentativeFather, r.BorrowerRepresentativeFamily);

        // عنوان المقترض/نوعه: يُقفلان إلا عند وجود ورثة أو ممثل للمقترض في الطلب (الفراغ حينها
        // مرافق مقصود للثابت «ورثة/ممثل ⟺ عنوان فارغ»)، فلا يُعدّه الحارس فرقًا — إعفاء حضور
        // (قرارات 11/13-15) يشمل الحفظ المتكرر بلا إضافات.
        if (!(requestedBorrowerHeirs.Count > 0 || borrowerHasRepInRequest))
        {
            RequireSame(errors, "عنوان المقترض", r.BorrowerAddress, doc.BorrowerAddress);
            RequireSame(errors, "نوع عنوان المقترض", r.BorrowerAddressType, doc.BorrowerAddressType);
        }

        if (isLegalBorrower && (requestedBorrowerHeirs.Count > 0 || borrowerHasRepInRequest))
        {
            errors.Add("لا يمكن إضافة ورثة أو ممثل شرعي على المقترض الاعتباري على الملف المناب");
        }
        else
        {
            ValidateMirrorHeirs(result, errors, requestedBorrowerHeirs,
                doc.Heirs.Where(h => h.GuarantorNumber is null).ToList(), borrowerHasRepInRequest,
                new MirrorHeirLabels(
                    "لا يمكن حذف وريث من الملف المناب",
                    "لا يمكن تعديل صفّة أو عنوان وريث قائم على الملف المناب"));

            // الممثل الشرعي للمقترض: تعبئة أولى عند فراغه مسموحة (إضافة محلية مضبوطة من المناب)؛
            // وتعديل قائم أو إزالته مرفوض (لا صمت — ApplyRequest يُصفّر القائم عند غياب الممثل
            // في الطلب فيُعدّ ذلك إزالةً صريحة).
            var borrowerStoredRepPresent = !DocumentText.IsEmptyRepresentative(
                doc.BorrowerRepresentativeName, doc.BorrowerRepresentativeFather, doc.BorrowerRepresentativeFamily);
            if (borrowerHasRepInRequest)
            {
                if (!borrowerStoredRepPresent)
                    result.NewRepresentativeLabels.Add("الممثل الشرعي للمقترض");
                else if (!BorrowerRepresentativeEquals(doc, r))
                    errors.Add("لا يمكن تعديل الممثل الشرعي للمقترض على الملف المناب");
            }
            else if (borrowerStoredRepPresent)
            {
                errors.Add("لا يمكن إزالة الممثل الشرعي للمقترض من الملف المناب");
            }
        }

        // الكفلاء: الدمج بالرقم المرجعي — لا إضافة كفيل جديد ولا حذف تحصيل ولا إعادة ترقيم،
        // والممثل الشرعي للكفيل إضافة محلية مسموحة (تُرصد للتنبيه العكسي عند تعبئته أول مرة)،
        // وورثة الكفيل إضافة-فقط بالمفتاح الهوياتي (الرقم + الثلاثي).
        ValidateMirrorGuarantors(result, errors, r.Guarantors, doc);

        // الأصول: الفرق القيمي فقط (إعادة إرسال المخزَّن القديم كما هو مقبولة).
        var requestedAssets = r.Assets.Select(DocumentText.BuildAsset).ToList();
        if (!AssetMultisetsEqual(requestedAssets, doc.Assets))
            errors.Add("لا يمكن تعديل الأموال على الملف المناب");

        if (errors.Count > 0)
            throw new ArgumentException($"لا يمكن تعديل الحقول المقفولة على الملف المناب: {string.Join("، ", errors)}");

        return new TargetEditReport(result.AddedHeirs, result.NewRepresentativeLabels);
    }
}
