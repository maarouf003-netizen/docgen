using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// «الملف المناب مرآةً للمنيب»: مزامنة نسخ من المنيب إلى المناب عند كل تعديل جوهري
/// (ref: docs/delegation-mirror-plan.md §3)، مع حارس تعديل المناب في UpdateAsync
/// (§3-ج) وتنبيهين متعاكسين مدموجين (§3-ب). كل التغييرات قيمية بلا سكيما وبلا هجرات.
/// </summary>
public sealed partial class DocumentService
{
    /// <summary>نية تنبيه مرآة (منيب ↔ مناب) تُطلق بعد نجاح معاملة الحفظ — الرسالة ملخص آخر تغيّر.</summary>
    private sealed record MirrorAlertIntent(
        int DelegationId,
        int RecipientLawyerId,
        int RecipientBranchId,
        int ActorUserId,
        string Message);

    /// <summary>حاصل فحص تعديل المناب: الإضافات المحلية المسموحة (ورثة/ممثل) للتنبيه العكسي.</summary>
    private sealed class TargetEditResult
    {
        public List<Heir> AddedHeirs { get; } = new();
        public List<string> NewRepresentativeLabels { get; } = new();
        public bool HasAdditions => AddedHeirs.Count > 0 || NewRepresentativeLabels.Count > 0;
    }

    // ── الحراسة: تعديل الملف المناب ────────────────────────────────────────────

    /// <summary>
    /// حارس تعديل المناب (دائم لكل مستندٍ ذي SourceDelegationId — حتى بعد الإتمام):
    /// يرفض أي فرق قيمي في الحقول المقفولة (الكتب/السند/المقترض/الجهات/الكفلاء/الأصول)،
    /// مع سماح الإضافات المحلية الوحيدة (وريث جديد، ممثل شرعي يُعبَّأ عند فراغه)،
    /// وتجاهل صامت لـ ImmediateActions (الحفظ الفارغ لا يمس السابق أصلًا).
    /// يُعاد الورثة الجدد لتوليد تنبيه المنيب بعد نجاح الحفظ.
    /// </summary>
    private static TargetEditResult ValidateDelegationTargetEdit(Document doc, DocumentUpsertRequest r)
    {
        var result = new TargetEditResult();
        if (doc.SourceDelegationId is null)
            return result;

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
        RequireSame(errors, "نوع الإلحاق", r.AnnexType, doc.AnnexType);
        RequireSame(errors, "رقم الإلحاق", r.AnnexNumber, doc.AnnexNumber);
        RequireSame(errors, "تاريخ الإلحاق", r.AnnexDate, doc.AnnexDate);
        RequireSame(errors, "نص الإدراج", r.InclusionText, doc.InclusionText);
        RequireSameNumeric(errors, "المبلغ (الأول)", r.AmountNumeric, doc.AmountNumeric);
        RequireSame(errors, "مبلغ العقد كتابة", r.AmountWords, doc.AmountWords);
        RequireSame(errors, "عملة المبلغ الأول", r.Currency, doc.Currency);
        RequireSameNumeric(errors, "المبلغ (الثاني)", r.Amount2Numeric, doc.Amount2Numeric);
        RequireSame(errors, "المبلغ الثاني كتابة", r.Amount2Words, doc.Amount2Words);
        RequireSame(errors, "عملة المبلغ الثاني", r.Currency2, doc.Currency2);
        RequireSameNumeric(errors, "المبلغ (الثالث)", r.Amount3Numeric, doc.Amount3Numeric);
        RequireSame(errors, "المبلغ الثالث كتابة", r.Amount3Words, doc.Amount3Words);
        RequireSame(errors, "عملة المبلغ الثالث", r.Currency3, doc.Currency3);
        RequireSameNumeric(errors, "المبلغ المدرج (الأول)", r.InclusionAmountNumeric, doc.InclusionAmountNumeric);
        RequireSame(errors, "المبلغ المدرج كتابة", r.InclusionAmountWords, doc.InclusionAmountWords);
        RequireSame(errors, "عملة المبلغ المدرج", r.InclusionCurrency, doc.InclusionCurrency);
        RequireSameNumeric(errors, "المبلغ المدرج (الثاني)", r.InclusionAmount2Numeric, doc.InclusionAmount2Numeric);
        RequireSame(errors, "المبلغ المدرج الثاني كتابة", r.InclusionAmount2Words, doc.InclusionAmount2Words);
        RequireSame(errors, "عملة المبلغ المدرج الثاني", r.InclusionCurrency2, doc.InclusionCurrency2);
        RequireSameNumeric(errors, "المبلغ المدرج (الثالث)", r.InclusionAmount3Numeric, doc.InclusionAmount3Numeric);
        RequireSame(errors, "المبلغ المدرج الثالث كتابة", r.InclusionAmount3Words, doc.InclusionAmount3Words);
        RequireSame(errors, "عملة المبلغ المدرج الثالث", r.InclusionCurrency3, doc.InclusionCurrency3);
        RequireSame(errors, "الدائرة", r.Court, doc.Court);
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
            NormalizeApplicantPublicEntities(r.ApplicantPublicEntities), r.Applicant);
        var storedEntities = EffectiveApplicantEntities(doc.ApplicantPublicEntities, doc.Applicant);
        if (!ApplicantEntitySetsEqual(requestedEntities, storedEntities))
            errors.Add("الجهات العامة طالبة التنفيذ مقفولة على الملف المناب");

        // ورثة المقترض والممثل الشرعي له: إضافة الناقص أو تعبئة الفراغ فقط — لا حذف ولا تعديل
        // لقائم، ولا ورثة ولا ممثل على مقترض اعتباري (ApplyRequest يُسقطها بصمت فيُرفض صراحةً).
        var borrowerNature = NormalizePartyNature(r.BorrowerNature);
        var isLegalBorrower = PartyNatureCatalog.IsLegal(borrowerNature);
        var requestedBorrowerHeirs = NormalizeHeirs(r.BorrowerHeirs, null);
        var borrowerHasRepInRequest = !IsEmptyRepresentative(
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
            ValidateMirrorBorrowerHeirs(result, errors, requestedBorrowerHeirs, doc.Heirs, borrowerHasRepInRequest);

            // الممثل الشرعي للمقترض: تعبئة أولى عند فراغه مسموحة (إضافة محلية مضبوطة من المناب)؛
            // وتعديل قائم أو إزالته مرفوض (لا صمت — ApplyRequest يُصفّر القائم عند غياب الممثل
            // في الطلب فيُعدّ ذلك إزالةً صريحة).
            var borrowerStoredRepPresent = !IsEmptyRepresentative(
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
        var requestedAssets = r.Assets.Select(BuildAsset).ToList();
        if (!AssetMultisetsEqual(requestedAssets, doc.Assets))
            errors.Add("لا يمكن تعديل الأموال على الملف المناب");

        if (errors.Count > 0)
            throw new ArgumentException($"لا يمكن تعديل الحقول المقفولة على الملف المناب: {string.Join("، ", errors)}");

        return result;
    }

    private static void ValidateMirrorGuarantors(
        TargetEditResult result, List<string> errors, IEnumerable<GuarantorDto> requestedDtos, Document doc)
    {
        var requested = requestedDtos.Select(BuildGuarantor).OrderBy(g => g.GuarantorNumber).ToList();
        var stored = doc.Guarantors.OrderBy(g => g.GuarantorNumber).ToList();
        var requestedNumbers = requested.Select(g => g.GuarantorNumber).ToList();
        var storedNumbers = stored.Select(g => g.GuarantorNumber).ToList();
        if (!requestedNumbers.SequenceEqual(storedNumbers))
        {
            var added = requestedNumbers.Except(storedNumbers).ToList();
            if (added.Count > 0)
                errors.Add($"لا يمكن إضافة كفيل جديد على الملف المناب (رقم الكفيل {added[0]})");
            if (storedNumbers.Except(requestedNumbers).Any())
                errors.Add("لا يمكن حذف كفيل من الملف المناب");
        }

        var storedByNumber = stored.ToDictionary(g => g.GuarantorNumber);
        var requestedDtosByNumber = requestedDtos.ToDictionary(g => g.GuarantorNumber);
        foreach (var g in requested)
        {
            if (!storedByNumber.TryGetValue(g.GuarantorNumber, out var current))
                continue;

            var dto = requestedDtosByNumber[g.GuarantorNumber];
            var requestedHeirs = NormalizeHeirs(dto.Heirs, g.GuarantorNumber);
            // تُقرأ حقول الممثل من صفّ الطلب مباشرةً لا من الكيان المُبنى (BuildGuarantor يُصفّر
            // ممثل الاعتباري مثل ApplyRequest) وإلا تعذّر على الحارس رؤية ممثلٍ على كفيل اعتباري
            // فيمر بصمت بدل الرفض الصريح (B3).
            var gHasRep = !IsEmptyRepresentative(dto.RepresentativeName, dto.RepresentativeFather, dto.RepresentativeFamily);
            var isLegalGuarantor = PartyNatureCatalog.IsLegal(g.GuarantorNature);

            // ورثة/ممثل على كفيل اعتباري: مرفوض صراحةً (ApplyRequest يُسقطها بصمت فتُجنَّب
            // التنبيهات الكاذبة — B3).
            if (isLegalGuarantor && (requestedHeirs.Count > 0 || gHasRep))
            {
                errors.Add($"لا يمكن إضافة ورثة أو ممثل شرعي على الكفيل الاعتباري {GuarantorFullName(g)} على الملف المناب");
                continue;
            }

            // المقارنة القيمية للنواة: عنوان الكفيل/نوعه يُستثنيان عند ورثة أو ممثل للكفيل في الطلب
            // (إعفاء حضور — B1).
            var exemptAddress = requestedHeirs.Count > 0 || gHasRep;
            if (!GuarantorCoreEquals(g, current, exemptAddress))
                errors.Add($"بيانات الكفيل {GuarantorFullName(g)} مقفولة على الملف المناب");

            // الممثل الشرعي للكفيل: تعبئة أولى عند فراغه مسموحة؛ وتعديل قائم أو إزالته مرفوض.
            var currentHasRep = !IsEmptyRepresentative(current.RepresentativeName, current.RepresentativeFather, current.RepresentativeFamily);
            if (gHasRep)
            {
                if (!currentHasRep)
                    result.NewRepresentativeLabels.Add($"الممثل الشرعي للكفيل {GuarantorFullName(g)}");
                else if (!GuarantorRepresentativeEquals(g, current))
                    errors.Add($"لا يمكن تعديل الممثل الشرعي للكفيل {GuarantorFullName(g)} على الملف المناب");
            }
            else if (currentHasRep)
            {
                errors.Add($"لا يمكن إزالة الممثل الشرعي للكفيل {GuarantorFullName(g)} من الملف المناب");
            }

            // ورثة الكفيل القائم: إضافة الناقص بالمفتاح الهوياتي فقط، وحذف/تعديل قائم مرفوض
            // (B2)، مع إعفاء عنوان الورثة فقط عند ممثلٍ للكفيل في الطلب (B1) — لا مجرد ورثة،
            // وإلا صارت أي معالجة لعنوان وريث قائم نفاذًا صامتًا بدل رفض.
            ValidateMirrorGuarantorHeirs(result, errors, g.GuarantorNumber, requestedHeirs, doc.Heirs, GuarantorFullName(g), gHasRep);
        }
    }

    private static void ValidateMirrorGuarantorHeirs(
        TargetEditResult result, List<string> errors,
        int? guarantorNumber, IReadOnlyList<Heir> requested,
        IEnumerable<Heir> stored, string guarantorLabel, bool exemptAddress)
    {
        var storedHeirs = stored.Where(h => h.GuarantorNumber == guarantorNumber).ToList();
        var storedKeys = storedHeirs.Select(HeirIdentityKey).ToHashSet();
        var requestedKeys = requested.Select(HeirIdentityKey).ToHashSet();

        // وريث جديد (هوية غير موجودة): إضافة محلية مسموحة تُخطر المنيب (مع تسمية الكفيل).
        foreach (var h in requested)
            if (!storedKeys.Contains(HeirIdentityKey(h)))
                result.AddedHeirs.Add(h);

        // حذف وريث قائم أو تغيير صفّته/عنوانه (نفس الهوية بقيمة مختلفة): مرفوض.
        foreach (var h in storedHeirs)
        {
            if (!requestedKeys.Contains(HeirIdentityKey(h)))
            {
                errors.Add($"لا يمكن حذف ورثة الكفيل {guarantorLabel} من الملف المناب");
                break;
            }
        }
        foreach (var h in requested)
        {
            var match = storedHeirs.FirstOrDefault(s => HeirIdentityKey(s) == HeirIdentityKey(h));
            if (match is not null && !HeirValueEquals(match, h, exemptAddress))
            {
                errors.Add($"لا يمكن تعديل ورثة الكفيل {guarantorLabel} على الملف المناب");
                break;
            }
        }
    }

    private static void ValidateMirrorBorrowerHeirs(
        TargetEditResult result, List<string> errors, IReadOnlyList<Heir> requested, IEnumerable<Heir> stored, bool exemptAddress)
    {
        var storedHeirs = stored.Where(h => h.GuarantorNumber is null).ToList();
        var storedKeys = storedHeirs.Select(HeirIdentityKey).ToHashSet();
        var requestedKeys = requested.Select(HeirIdentityKey).ToHashSet();

        // الوريث الجديد (هوية غير موجودة): إضافة محلية مسموحة تُخطر المنيب.
        foreach (var h in requested)
            if (!storedKeys.Contains(HeirIdentityKey(h)))
                result.AddedHeirs.Add(h);

        // حذف وريث قائم أو تغيير صفّته/عنوانه (نفس الهوية بقيمة مختلفة): مرفوض.
        foreach (var h in storedHeirs)
        {
            if (!requestedKeys.Contains(HeirIdentityKey(h)))
            {
                errors.Add("لا يمكن حذف وريث من الملف المناب");
                break;
            }
        }
        foreach (var h in requested)
        {
            var match = storedHeirs.FirstOrDefault(s => HeirIdentityKey(s) == HeirIdentityKey(h));
            if (match is not null && !HeirValueEquals(match, h, exemptAddress))
            {
                errors.Add("لا يمكن تعديل صفّة أو عنوان وريث قائم على الملف المناب");
                break;
            }
        }
    }

    private static void RequireSame(List<string> errors, string label, string? requested, string? current)
    {
        if (!string.Equals(Trimmed(requested), Trimmed(current), StringComparison.Ordinal))
            errors.Add($"«{label}» مقفول على الملف المناب");
    }

    private static void RequireSameNumeric(List<string> errors, string label, decimal? requested, decimal? current)
    {
        if (requested != current)
            errors.Add($"«{label}» مقفول على الملف المناب");
    }

    /// <summary>مفتاح هوية الوريث: رقم الكفيل + الاسم الثلاثي (لا عنوان ولا صفة) — أساس «إضافة الناقص».</summary>
    private static (int?, string, string, string) HeirIdentityKey(Heir h) =>
        (
            h.GuarantorNumber,
            Trimmed(h.HeirName) ?? string.Empty,
            Trimmed(h.HeirFather) ?? string.Empty,
            Trimmed(h.HeirFamily) ?? string.Empty);

    /// <summary>هل تطابق قيم الوريث التامة (الصفة ونوع العنوان والعنوان)؟— لتأكيد بقائه كما هو.
    /// عند إعفاء العنوان (ورثة/ممثل على الطرف في الطلب) يُقارن الصفة فقط لأن العنوان/نوعه
    /// يُفرّغان مرافقين متى وُجد ورثة أو ممثل (قرارات 11/13-15).</summary>
    private static bool HeirValueEquals(Heir a, Heir b, bool exemptAddress) =>
        string.Equals(Trimmed(a.HeirCapacity), Trimmed(b.HeirCapacity), StringComparison.Ordinal)
        && (exemptAddress
            || (string.Equals(Trimmed(a.AddressType), Trimmed(b.AddressType), StringComparison.Ordinal)
                && string.Equals(Trimmed(a.HeirAddress), Trimmed(b.HeirAddress), StringComparison.Ordinal)));

    /// <summary>هل تطابقت قيم الكفيل غير الممثل؟ (الدمج بالرقم المرجعي لا المساس بالحقول الممثلة).
    /// عند إعفاء العنوان (ورثة/ممثل على هذا الكفيل في الطلب) يُستثنى عنوانه ونوعه من المقارنة.</summary>
    private static bool GuarantorCoreEquals(Guarantor a, Guarantor b, bool exemptAddress = false) =>
        string.Equals(Trimmed(a.GuarantorName), Trimmed(b.GuarantorName), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.GuarantorFather), Trimmed(b.GuarantorFather), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.GuarantorFamily), Trimmed(b.GuarantorFamily), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.GuarantorMother), Trimmed(b.GuarantorMother), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.GuarantorBirth), Trimmed(b.GuarantorBirth), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.GuarantorRegister), Trimmed(b.GuarantorRegister), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.GuarantorNationalId), Trimmed(b.GuarantorNationalId), StringComparison.Ordinal)
        && (exemptAddress
            || (string.Equals(Trimmed(a.GuarantorAddress), Trimmed(b.GuarantorAddress), StringComparison.Ordinal)
                && string.Equals(Trimmed(a.AddressType), Trimmed(b.AddressType), StringComparison.Ordinal)))
        && string.Equals(Trimmed(a.GuarantorNature), Trimmed(b.GuarantorNature), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.GuarantorRegistrationNumber), Trimmed(b.GuarantorRegistrationNumber), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.GuarantorRepresentedBy), Trimmed(b.GuarantorRepresentedBy), StringComparison.Ordinal);

    /// <summary>هل تطابقت حقول الممثل الشرعي للكفيل المُطبَّعة (المخزَّنة مقابل المُرسَلة)?</summary>
    private static bool GuarantorRepresentativeEquals(Guarantor a, Guarantor b) =>
        string.Equals(Trimmed(a.RepresentativeName), Trimmed(b.RepresentativeName), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.RepresentativeFather), Trimmed(b.RepresentativeFather), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.RepresentativeFamily), Trimmed(b.RepresentativeFamily), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.RepresentativeCapacity), Trimmed(b.RepresentativeCapacity), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.RepresentativeAddressType), Trimmed(b.RepresentativeAddressType), StringComparison.Ordinal)
        && string.Equals(Trimmed(a.RepresentativeAddress), Trimmed(b.RepresentativeAddress), StringComparison.Ordinal);

    /// <summary>هل الممثل الشرعي للمقترض المخزَّن يطابق ما أُرسل (بعد التطبيع نفسه الذي يعتمده الحفظ)?</summary>
    private static bool BorrowerRepresentativeEquals(Document d, DocumentUpsertRequest r)
    {
        var requested = BorrowerRepresentativeRequestValues(r);
        return string.Equals(Trimmed(d.BorrowerRepresentativeName), requested.RepresentativeName, StringComparison.Ordinal)
            && string.Equals(Trimmed(d.BorrowerRepresentativeFather), requested.RepresentativeFather, StringComparison.Ordinal)
            && string.Equals(Trimmed(d.BorrowerRepresentativeFamily), requested.RepresentativeFamily, StringComparison.Ordinal)
            && string.Equals(Trimmed(d.BorrowerRepresentativeCapacity), requested.RepresentativeCapacity, StringComparison.Ordinal)
            && string.Equals(Trimmed(d.BorrowerRepresentativeAddressType), requested.RepresentativeAddressType, StringComparison.Ordinal)
            && string.Equals(Trimmed(d.BorrowerRepresentativeAddress), requested.RepresentativeAddress, StringComparison.Ordinal);
    }

    /// <summary>حقول الممثل الشرعي للمقترض في الطلب مُطبَّعة كما يخزّنها ApplyRequest (صفرة عند الغياب).</summary>
    private static (string? RepresentativeName, string? RepresentativeFather, string? RepresentativeFamily,
        string? RepresentativeCapacity, string? RepresentativeAddressType, string? RepresentativeAddress)
        BorrowerRepresentativeRequestValues(DocumentUpsertRequest r)
    {
        if (IsEmptyRepresentative(r.BorrowerRepresentativeName, r.BorrowerRepresentativeFather, r.BorrowerRepresentativeFamily))
            return (null, null, null, null, null, null);
        return (
            (r.BorrowerRepresentativeName ?? string.Empty).Trim(),
            (r.BorrowerRepresentativeFather ?? string.Empty).Trim(),
            (r.BorrowerRepresentativeFamily ?? string.Empty).Trim(),
            NormalizeRepresentativeCapacity(r.BorrowerRepresentativeCapacity),
            NormalizeRepresentativeAddressType(r.BorrowerRepresentativeAddressType),
            (r.BorrowerRepresentativeAddress ?? string.Empty).Trim());
    }

    private static string GuarantorFullName(Guarantor g) => string.Join(' ',
        new[] { g.GuarantorName, g.GuarantorFather, g.GuarantorFamily }.Where(v => !string.IsNullOrWhiteSpace(v)));

    /// <summary>القائمة الفعلية كما يخزّنها ApplyRequest: القائمة غير الفارغة، أو الرجوع إلى
    /// نص «طالب التنفيذ» كجهة واحدة عند غيابها (توافق الطلبات القديمة) — تُطبَّق على جهتي
    /// الطلب والمخزَّن معًا فيطابق الحارسُ ما سيُخزَّن فعلًا من كلا الجانبين (T4/B6).</summary>
    private static List<ApplicantPublicEntity> EffectiveApplicantEntities(
        IEnumerable<ApplicantPublicEntity> normalized, string? applicantText)
    {
        var result = normalized.ToList();
        if (result.Count > 0 || string.IsNullOrWhiteSpace(applicantText))
            return result;
        result.Add(new ApplicantPublicEntity { Name = applicantText.Trim() });
        return result;
    }

    /// <summary>المطابقة على RegistryId ثم الاسم، مقارنةً كمجموعة (بلا ترتيب).</summary>
    private static bool ApplicantEntitySetsEqual(IEnumerable<ApplicantPublicEntity> a, IEnumerable<ApplicantPublicEntity> b)
    {
        var x = a.ToList();
        var y = b.ToList();
        if (x.Count != y.Count)
            return false;
        var consumed = new bool[x.Count];
        foreach (var entity in y)
        {
            var matched = false;
            for (var i = 0; i < x.Count; i++)
            {
                if (consumed[i])
                    continue;
                if (ApplicantEntityMatches(x[i], entity))
                {
                    consumed[i] = true;
                    matched = true;
                    break;
                }
            }
            if (!matched)
                return false;
        }
        return true;
    }

    private static bool ApplicantEntityMatches(ApplicantPublicEntity a, ApplicantPublicEntity b)
    {
        var sameRegistry = a.RegistryId is not null && b.RegistryId is not null && a.RegistryId == b.RegistryId;
        var sameName = string.Equals(Trimmed(a.Name), Trimmed(b.Name), StringComparison.Ordinal);
        if (!sameRegistry && !sameName)
            return false;
        return string.Equals(Trimmed(a.Branch), Trimmed(b.Branch), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.Governorate), Trimmed(b.Governorate), StringComparison.Ordinal);
    }

    /// <summary>مقارنة الأصول كمجموعة بمعناها القيمي (بعد التطبيع نفسه الذي يعتمده الحفظ).</summary>
    private static bool AssetMultisetsEqual(IEnumerable<Asset> a, IEnumerable<Asset> b)
    {
        var x = a.ToList();
        var y = b.ToList();
        if (x.Count != y.Count)
            return false;
        var consumed = new bool[x.Count];
        foreach (var asset in y)
        {
            var matched = false;
            for (var i = 0; i < x.Count; i++)
            {
                if (consumed[i])
                    continue;
                if (AssetsEqual(x[i], asset))
                {
                    consumed[i] = true;
                    matched = true;
                    break;
                }
            }
            if (!matched)
                return false;
        }
        return true;
    }

    private static bool AssetsEqual(Asset a, Asset b)
    {
        if (!string.Equals(Trimmed(a.AssetKind), Trimmed(b.AssetKind), StringComparison.Ordinal))
            return false;
        if (!string.Equals(Trimmed(a.ShareType), Trimmed(b.ShareType), StringComparison.Ordinal))
            return false;
        if (!SameOwners(a.Owners, b.Owners))
            return false;
        return string.Equals(Trimmed(a.Property), Trimmed(b.Property), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.PropertyNumber), Trimmed(b.PropertyNumber), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.PropertyDistrict), Trimmed(b.PropertyDistrict), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.LandRegistry), Trimmed(b.LandRegistry), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.VehicleType), Trimmed(b.VehicleType), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.VehicleClass), Trimmed(b.VehicleClass), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.PlateNumber), Trimmed(b.PlateNumber), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.VehicleGovernorate), Trimmed(b.VehicleGovernorate), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.RegisterNumber), Trimmed(b.RegisterNumber), StringComparison.Ordinal)
            && a.RegistrationDate == b.RegistrationDate
            && string.Equals(Trimmed(a.ShopGovernorate), Trimmed(b.ShopGovernorate), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.ShopDescription), Trimmed(b.ShopDescription), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.ShopLocation), Trimmed(b.ShopLocation), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.PublicEntity), Trimmed(b.PublicEntity), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.LicenseNumber), Trimmed(b.LicenseNumber), StringComparison.Ordinal)
            && a.LicenseDate == b.LicenseDate
            && string.Equals(Trimmed(a.LicenseIssuer), Trimmed(b.LicenseIssuer), StringComparison.Ordinal)
            && string.Equals(Trimmed(a.Notes), Trimmed(b.Notes), StringComparison.Ordinal)
            && a.SeizureDate == b.SeizureDate;
    }

    private static bool SameOwners(IEnumerable<AssetOwner> a, IEnumerable<AssetOwner> b)
    {
        var anames = a.OrderBy(o => o.Order).Select(o => o.Name).ToList();
        var bnames = b.OrderBy(o => o.Order).Select(o => o.Name).ToList();
        if (anames.Count != bnames.Count)
            return false;
        for (var i = 0; i < anames.Count; i++)
        {
            if (!string.Equals(Trimmed(anames[i]), Trimmed(bnames[i]), StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? DateText(DateTime? value) =>
        value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // ── المزامنة: منيب → مناب (نسخ قيمي بلا حذف إلا استثناء الكفيل المحذوف) ──────────

    /// <summary>
    /// يُستدعى داخل معاملة UpdateAsync بعد حفظ المنيب. تنسخ المرآة الحقول المقفولة إلى كل
    /// منابٍ لإضافةٍ معلّقة (غير منفذة)، وتُعيد اشتقاق بيانات المناب (FillDerivedFields)،
    /// وتُعيد نوايا التنبيه المُتحدِدة لتُطلق بعد نجاح المعاملة (نقطة خروج واحدة).
    /// </summary>
    private async Task<List<MirrorAlertIntent>> MirrorSourceEditsToTargetsAsync(Document source, CancellationToken token)
    {
        var delegations = await _delegations.ListPendingBySourceWithTargetsAsync(source.Id, token);
        if (delegations.Count == 0)
            return new List<MirrorAlertIntent>();

        var intents = new List<MirrorAlertIntent>();
        foreach (var delegation in delegations)
        {
            var target = delegation.TargetDocument;
            if (target is null)
                continue;

            var labels = new List<string>();
            MirrorBorrowerInto(source, target, labels);
            MirrorBondInto(source, target, labels);
            MirrorBooksInto(source, target, labels);
            MirrorEntitiesInto(source, target, labels);
            MirrorGuarantorsInto(source, target, labels);
            MirrorBorrowerHeirsInto(source, target, labels);
            // الأصول غير مُزامَنة: ملك العرض/البيع المحلي للمناب، والحارس يرفض تغييرها قيميًا.

            if (labels.Count == 0)
                continue;

            FillDerivedFields(target);
            await _uow.SaveChangesAsync(token);

            var recipientBranch = target.BranchId ?? source.BranchId ?? delegation.ExternalBranchId;
            intents.Add(new MirrorAlertIntent(
                delegation.Id,
                target.CreatedById,
                recipientBranch ?? 0,
                source.CreatedById,
                $"تم تحديث نسخة الملف المناب لمواكبة الملف المنيب: {string.Join("، ", labels)}"));
        }

        return intents;
    }

    private static void MirrorBorrowerInto(Document source, Document target, List<string> labels)
    {
        // عنوان المقترض/نوعه لا يُكتبان على منابٍ له ورثة أو ممثل محلي (الفراغ مرافق مقصود للثابت
        // «ورثة/ممثل ⟺ عنوان فارغ») — التخطي صامت بلا تنبيه (قرار 17).
        var targetHasLocalFamily = target.Heirs.Any(h => h.GuarantorNumber is null)
            || !IsEmptyRepresentative(target.BorrowerRepresentativeName, target.BorrowerRepresentativeFather, target.BorrowerRepresentativeFamily);

        var changed = false;
        changed |= CopyField(source.BorrowerName, target.BorrowerName, v => target.BorrowerName = v);
        changed |= CopyField(source.BorrowerFather, target.BorrowerFather, v => target.BorrowerFather = v);
        changed |= CopyField(source.BorrowerFamily, target.BorrowerFamily, v => target.BorrowerFamily = v);
        changed |= CopyField(source.BorrowerMother, target.BorrowerMother, v => target.BorrowerMother = v);
        changed |= CopyField(source.BorrowerBirth, target.BorrowerBirth, v => target.BorrowerBirth = v);
        changed |= CopyField(source.BorrowerRegister, target.BorrowerRegister, v => target.BorrowerRegister = v);
        changed |= CopyField(source.BorrowerNationalId, target.BorrowerNationalId, v => target.BorrowerNationalId = v);
        if (!targetHasLocalFamily)
        {
            changed |= CopyField(source.BorrowerAddress, target.BorrowerAddress, v => target.BorrowerAddress = v);
            changed |= CopyField(source.BorrowerAddressType, target.BorrowerAddressType, v => target.BorrowerAddressType = v);
        }
        changed |= CopyField(source.BorrowerNature, target.BorrowerNature,
            v => target.BorrowerNature = v ?? PartyNatureCatalog.Natural);
        changed |= CopyField(source.BorrowerRegistrationNumber, target.BorrowerRegistrationNumber, v => target.BorrowerRegistrationNumber = v);
        changed |= CopyField(source.BorrowerRepresentedBy, target.BorrowerRepresentedBy, v => target.BorrowerRepresentedBy = v);
        if (changed)
            labels.Add("بيانات المقترض");

        // الممثل الشرعي للمقترض: تعبئة عند الفراغ فقط — لا مساس بالمضبوط محليًا.
        if (IsEmptyRepresentative(target.BorrowerRepresentativeName, target.BorrowerRepresentativeFather, target.BorrowerRepresentativeFamily)
            && !IsEmptyRepresentative(source.BorrowerRepresentativeName, source.BorrowerRepresentativeFather, source.BorrowerRepresentativeFamily))
        {
            target.BorrowerRepresentativeName = source.BorrowerRepresentativeName;
            target.BorrowerRepresentativeFather = source.BorrowerRepresentativeFather;
            target.BorrowerRepresentativeFamily = source.BorrowerRepresentativeFamily;
            target.BorrowerRepresentativeCapacity = source.BorrowerRepresentativeCapacity;
            target.BorrowerRepresentativeAddressType = source.BorrowerRepresentativeAddressType;
            target.BorrowerRepresentativeAddress = source.BorrowerRepresentativeAddress;
            labels.Add("الممثل الشرعي للمقترض");
        }
    }

    private static void MirrorBondInto(Document source, Document target, List<string> labels)
    {
        var changed = false;
        changed |= CopyField(source.ContractType, target.ContractType, v => target.ContractType = v);
        changed |= CopyField(source.ContractTypeSelector, target.ContractTypeSelector, v => target.ContractTypeSelector = v);
        changed |= CopyField(source.ContractNumber, target.ContractNumber, v => target.ContractNumber = v);
        changed |= CopyField(source.ContractDate, target.ContractDate, v => target.ContractDate = v);
        changed |= CopyField(source.AnnexType, target.AnnexType, v => target.AnnexType = v);
        changed |= CopyField(source.AnnexNumber, target.AnnexNumber, v => target.AnnexNumber = v);
        changed |= CopyField(source.AnnexDate, target.AnnexDate, v => target.AnnexDate = v);
        changed |= CopyField(source.InclusionText, target.InclusionText, v => target.InclusionText = v);
        changed |= CopyNumeric(source.AmountNumeric, target.AmountNumeric, v => target.AmountNumeric = v);
        changed |= CopyField(source.AmountWords, target.AmountWords, v => target.AmountWords = v);
        changed |= CopyField(source.Currency, target.Currency, v => target.Currency = v);
        changed |= CopyNumeric(source.Amount2Numeric, target.Amount2Numeric, v => target.Amount2Numeric = v);
        changed |= CopyField(source.Amount2Words, target.Amount2Words, v => target.Amount2Words = v);
        changed |= CopyField(source.Currency2, target.Currency2, v => target.Currency2 = v);
        changed |= CopyNumeric(source.Amount3Numeric, target.Amount3Numeric, v => target.Amount3Numeric = v);
        changed |= CopyField(source.Amount3Words, target.Amount3Words, v => target.Amount3Words = v);
        changed |= CopyField(source.Currency3, target.Currency3, v => target.Currency3 = v);
        changed |= CopyNumeric(source.InclusionAmountNumeric, target.InclusionAmountNumeric, v => target.InclusionAmountNumeric = v);
        changed |= CopyField(source.InclusionAmountWords, target.InclusionAmountWords, v => target.InclusionAmountWords = v);
        changed |= CopyField(source.InclusionCurrency, target.InclusionCurrency, v => target.InclusionCurrency = v);
        changed |= CopyNumeric(source.InclusionAmount2Numeric, target.InclusionAmount2Numeric, v => target.InclusionAmount2Numeric = v);
        changed |= CopyField(source.InclusionAmount2Words, target.InclusionAmount2Words, v => target.InclusionAmount2Words = v);
        changed |= CopyField(source.InclusionCurrency2, target.InclusionCurrency2, v => target.InclusionCurrency2 = v);
        changed |= CopyNumeric(source.InclusionAmount3Numeric, target.InclusionAmount3Numeric, v => target.InclusionAmount3Numeric = v);
        changed |= CopyField(source.InclusionAmount3Words, target.InclusionAmount3Words, v => target.InclusionAmount3Words = v);
        changed |= CopyField(source.InclusionCurrency3, target.InclusionCurrency3, v => target.InclusionCurrency3 = v);
        changed |= CopyField(source.Court, target.Court, v => target.Court = v);
        changed |= CopyField(source.Applicant, target.Applicant, v => target.Applicant = v);
        if (changed)
            labels.Add("السند التنفيذي");
    }

    private static void MirrorBooksInto(Document source, Document target, List<string> labels)
    {
        var changed = false;
        changed |= CopyField(source.FileArrivalNumber, target.FileArrivalNumber, v => target.FileArrivalNumber = v);
        changed |= CopyField(source.FileArrivalDate, target.FileArrivalDate, v => target.FileArrivalDate = v);
        changed |= CopyField(source.FileIncoming, target.FileIncoming, v => target.FileIncoming = v);
        changed |= CopyField(source.FileIncomingDate, target.FileIncomingDate, v => target.FileIncomingDate = v);
        changed |= CopyField(source.UnderFilingNumber, target.UnderFilingNumber, v => target.UnderFilingNumber = v);
        changed |= CopyField(source.FileReceiptNumber, target.FileReceiptNumber, v => target.FileReceiptNumber = v);
        changed |= CopyNullableDate(source.FileReceiptDate, target.FileReceiptDate, v => target.FileReceiptDate = v);
        changed |= CopyField(source.SeizureDate, target.SeizureDate, v => target.SeizureDate = v);
        if (changed)
            labels.Add("الكتب");
    }

    private static void MirrorEntitiesInto(Document source, Document target, List<string> labels)
    {
        var changed = false;
        var currentEntities = target.ApplicantPublicEntities.ToList();
        var consumed = new bool[currentEntities.Count];
        var next = new List<ApplicantPublicEntity>();
        foreach (var entity in source.ApplicantPublicEntities)
        {
            var matchIndex = -1;
            for (var i = 0; i < currentEntities.Count; i++)
            {
                if (consumed[i])
                    continue;
                var candidate = currentEntities[i];
                if (ApplicantEntitySameIdentity(entity, candidate))
                {
                    consumed[i] = true;
                    matchIndex = i;
                    break;
                }
            }
            if (matchIndex >= 0)
            {
                var current = currentEntities[matchIndex];
                changed |= CopyField(entity.Name, current.Name, v => current.Name = v);
                changed |= CopyField(entity.Branch, current.Branch, v => current.Branch = v);
                changed |= CopyField(entity.Governorate, current.Governorate, v => current.Governorate = v);
                if (current.RegistryId != entity.RegistryId)
                {
                    current.RegistryId = entity.RegistryId;
                    changed = true;
                }
                next.Add(current);
            }
            else
            {
                next.Add(new ApplicantPublicEntity
                {
                    Name = entity.Name,
                    Branch = entity.Branch,
                    Governorate = entity.Governorate,
                    RegistryId = entity.RegistryId,
                });
                changed = true;
            }
        }
        if (next.Count != currentEntities.Count)
            changed = true;

        target.ApplicantPublicEntities.Clear();
        foreach (var entity in next)
            target.ApplicantPublicEntities.Add(entity);
        if (changed)
            labels.Add("الجهات العامة طالبة التنفيذ");
    }

    /// <summary>هوية الجهة للمطابقة: RegistryId المتساوي أولاً ثم الاسم.</summary>
    private static bool ApplicantEntitySameIdentity(ApplicantPublicEntity a, ApplicantPublicEntity b) =>
        (a.RegistryId is not null && b.RegistryId is not null && a.RegistryId == b.RegistryId)
        || string.Equals(Trimmed(a.Name), Trimmed(b.Name), StringComparison.Ordinal);

    private static void MirrorGuarantorsInto(Document source, Document target, List<string> labels)
    {
        var changed = false;
        var deletedNames = new List<string>();
        var sourceNumbers = source.Guarantors.Select(g => g.GuarantorNumber).ToHashSet();

        // الكفلاء الحاليون على المنيب: تحديث أو إضافة (بالرقم المرجعي المستقر — لا يُعاد ترقيمه).
        foreach (var g in source.Guarantors.OrderBy(g => g.GuarantorNumber))
        {
            var current = target.Guarantors.FirstOrDefault(t => t.GuarantorNumber == g.GuarantorNumber);
            if (current is not null)
            {
                // عنوان الكفيل/نوعه لا يُكتبان على منابٍ له ورثة أو ممثل محلي لهذا الكفيل (فراغ
                // مرافق مقصود — التخطي صامت بلا تنبيه).
                var currentHasLocalFamily = target.Heirs.Any(h => h.GuarantorNumber == g.GuarantorNumber)
                    || !IsEmptyRepresentative(current.RepresentativeName, current.RepresentativeFather, current.RepresentativeFamily);
                changed |= CopyGuarantorCoreFields(g, current, currentHasLocalFamily);
                // الممثل الشرعي للكفيل: تعبئة عند الفراغ فقط (لئلا تُطمس الإضافة المحلية).
                if (IsEmptyRepresentative(current.RepresentativeName, current.RepresentativeFather, current.RepresentativeFamily)
                    && !IsEmptyRepresentative(g.RepresentativeName, g.RepresentativeFather, g.RepresentativeFamily))
                {
                    current.RepresentativeName = g.RepresentativeName;
                    current.RepresentativeFather = g.RepresentativeFather;
                    current.RepresentativeFamily = g.RepresentativeFamily;
                    current.RepresentativeCapacity = g.RepresentativeCapacity;
                    current.RepresentativeAddressType = g.RepresentativeAddressType;
                    current.RepresentativeAddress = g.RepresentativeAddress;
                    changed = true;
                }
                // ورثة الكفيل القائم: إضافة الناقص بلا حذف (الإضافات المحلية محفوظة).
                changed |= MirrorHeirsAddOnly(source, target, g.GuarantorNumber);
                continue;
            }

            // كفيل جديد على المنيب: يُنسخ كاملًا مع ورثته المرتبطة برقمه.
            target.Guarantors.Add(DocumentDelegationService.CopyGuarantor(g));
            foreach (var h in source.Heirs.Where(h => h.GuarantorNumber == g.GuarantorNumber))
                target.Heirs.Add(DocumentDelegationService.CopyHeir(h));
            changed = true;
        }

        // الكفلاء المحذوفون من المنيب: حذف من المناب مع ورثتهم المرتبطة برقمهم
        // (الاستثناء الموثق الوحيد لقاعدة «بلا حذف» — وإلا علقت صفوف يتيمة).
        foreach (var stale in target.Guarantors.Where(g => !sourceNumbers.Contains(g.GuarantorNumber)).ToList())
        {
            var removedHeirs = target.Heirs.Where(h => h.GuarantorNumber == stale.GuarantorNumber).ToList();
            foreach (var h in removedHeirs)
                target.Heirs.Remove(h);
            target.Guarantors.Remove(stale);
            changed = true;

            // تُسمَّى الورثة المحذوفون مع الكفيل المحذوف (قرار 8): بالأسماء أولاً وإلا بالعدد.
            var label = GuarantorFullName(stale);
            if (removedHeirs.Count > 0)
            {
                var heirNames = removedHeirs
                    .Select(h => string.Join(' ', new[] { h.HeirName, h.HeirFather, h.HeirFamily }.Where(v => !string.IsNullOrWhiteSpace(v))))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
                label += heirNames.Count > 0
                    ? $" وورثته ({string.Join("، ", heirNames)})"
                    : $" وورثته ({removedHeirs.Count})";
            }
            deletedNames.Add(label);
        }

        if (changed)
            labels.Add(deletedNames.Count > 0
                ? $"الكفلاء (حُذف من المنيب: {string.Join("، ", deletedNames)})"
                : "الكفلاء");
    }

    private static bool MirrorHeirsAddOnly(Document source, Document target, int? guarantorNumber)
    {
        var added = false;
        var existingKeys = target.Heirs
            .Where(h => h.GuarantorNumber == guarantorNumber)
            .Select(HeirIdentityKey)
            .ToHashSet();
        foreach (var h in source.Heirs.Where(h => h.GuarantorNumber == guarantorNumber))
        {
            if (existingKeys.Contains(HeirIdentityKey(h)))
                continue;
            target.Heirs.Add(DocumentDelegationService.CopyHeir(h));
            existingKeys.Add(HeirIdentityKey(h));
            added = true;
        }
        return added;
    }

    private static void MirrorBorrowerHeirsInto(Document source, Document target, List<string> labels)
    {
        // ورثة المنيب (دون رقم كفيل): إضافة الناقص بالمطابقة القيمية (الرقم + الاسم الثلاثي) بلا حذف
        // — التعديل/الحذف على المنيب لا يمس المناب (قرار 8) ويُخطر المُناب عبر التنبيه المدموج.
        var added = false;
        var existingKeys = target.Heirs.Where(h => h.GuarantorNumber is null).Select(HeirIdentityKey).ToHashSet();
        foreach (var h in source.Heirs.Where(h => h.GuarantorNumber is null))
        {
            if (existingKeys.Contains(HeirIdentityKey(h)))
                continue;
            target.Heirs.Add(DocumentDelegationService.CopyHeir(h));
            existingKeys.Add(HeirIdentityKey(h));
            added = true;
        }
        if (added)
            labels.Add("ورثة المقترض");
    }

    /// <summary>نسخ قيم الكفيل غير الممثلة (الدمج بالرقم المرجعي لا يمسّ الممثل المضبوط محليًا).
    /// عند `skipAddress` (ورثة/ممثل محليون على الكفيل) لا يُكتب عنوانه ونوعه (فراغ مرافق مقصود).</summary>
    private static bool CopyGuarantorCoreFields(Guarantor source, Guarantor target, bool skipAddress)
    {
        var changed = false;
        changed |= CopyField(source.GuarantorName, target.GuarantorName, v => target.GuarantorName = v);
        changed |= CopyField(source.GuarantorFather, target.GuarantorFather, v => target.GuarantorFather = v);
        changed |= CopyField(source.GuarantorFamily, target.GuarantorFamily, v => target.GuarantorFamily = v);
        changed |= CopyField(source.GuarantorMother, target.GuarantorMother, v => target.GuarantorMother = v);
        changed |= CopyField(source.GuarantorBirth, target.GuarantorBirth, v => target.GuarantorBirth = v);
        changed |= CopyField(source.GuarantorRegister, target.GuarantorRegister, v => target.GuarantorRegister = v);
        changed |= CopyField(source.GuarantorNationalId, target.GuarantorNationalId, v => target.GuarantorNationalId = v);
        if (!skipAddress)
        {
            changed |= CopyField(source.GuarantorAddress, target.GuarantorAddress, v => target.GuarantorAddress = v);
            changed |= CopyField(source.AddressType, target.AddressType, v => target.AddressType = v);
        }
        changed |= CopyField(source.GuarantorNature, target.GuarantorNature,
            v => target.GuarantorNature = v ?? PartyNatureCatalog.Natural);
        changed |= CopyField(source.GuarantorRegistrationNumber, target.GuarantorRegistrationNumber, v => target.GuarantorRegistrationNumber = v);
        changed |= CopyField(source.GuarantorRepresentedBy, target.GuarantorRepresentedBy, v => target.GuarantorRepresentedBy = v);
        return changed;
    }

    private static bool CopyField(string? source, string? current, Action<string?> assign)
    {
        if (string.Equals(Trimmed(source), Trimmed(current), StringComparison.Ordinal))
            return false;
        assign(Trimmed(source));
        return true;
    }

    private static bool CopyNumeric(decimal source, decimal current, Action<decimal> assign)
    {
        if (source == current)
            return false;
        assign(source);
        return true;
    }

    private static bool CopyNullableDate(DateTime? source, DateTime? current, Action<DateTime?> assign)
    {
        if (source == current)
            return false;
        assign(source);
        return true;
    }

    // ── إطلاق التنبيهات (بعد نجاح المعاملة — عزل الفشل بنمط head_alert_failed) ──────

    /// <summary>إطلاق تنبيهات المرآة المجمعة بعد نجاح معاملة الحفظ (كل فشل معزول ولا يُفشل الحفظ).</summary>
    private async Task FireDelegationAlertsAsync(IReadOnlyList<MirrorAlertIntent> intents, CancellationToken ct)
    {
        foreach (var intent in intents)
            await FireDelegationAlertAsync(intent, ct);
    }

    /// <summary>تنبيه المرآة الفردي معزولًا: فشله يقع في سجل التدقيق ولا يمسّ نتيجة الحفظ.</summary>
    private async Task FireDelegationAlertAsync(MirrorAlertIntent intent, CancellationToken ct)
    {
        try
        {
            await _alertService.UpsertDelegationMessageAsync(
                intent.DelegationId, intent.RecipientLawyerId,
                intent.ActorUserId, intent.RecipientBranchId, intent.Message, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(null, "head_alert_failed", intent.DelegationId, null,
                $"فشل تنبيه المرآة للمحامي {intent.RecipientLawyerId}: {ex.Message}", ct);
        }
    }

    /// <summary>تنبيه تغيّر حالة المنيب إلى مناباته المعلقة (مسار StatusChangeModal المنفصل عن UpdateAsync).</summary>
    private async Task FireDelegationStatusChangeAlertsAsync(Document doc, CancellationToken ct)
    {
        List<DocumentDelegation> delegations;
        try
        {
            delegations = await _delegations.ListPendingBySourceWithTargetsAsync(doc.Id, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(null, "head_alert_failed", doc.Id, doc.DocumentType, ex.Message, ct);
            return;
        }

        var recipientBranch = doc.BranchId;
        foreach (var delegation in delegations)
        {
            var target = delegation.TargetDocument;
            if (target is null)
                continue;
            var intent = new MirrorAlertIntent(
                delegation.Id,
                target.CreatedById,
                target.BranchId ?? recipientBranch ?? delegation.ExternalBranchId ?? 0,
                doc.CreatedById,
                $"تغيّرت حالة الملف المنيب إلى «{DocumentStatusResolver.Resolve(doc)}»");
            await FireDelegationAlertAsync(intent, ct);
        }
    }

    /// <summary>إطلاق تنبيه المنيب عند الإضافات المحلية (وريث/ممثل) على الملف المناب معزولًا.</summary>
    private async Task FireDelegationReverseAlertAsync(int targetDocumentId, Document target, TargetEditResult result, CancellationToken ct)
    {
        DocumentDelegation? delegation = null;
        try
        {
            delegation = await _delegations.FindByTargetAsync(targetDocumentId, ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(null, "head_alert_failed", targetDocumentId, null, ex.Message, ct);
            return;
        }

        if (delegation?.SourceDocument is null)
            return;

        var source = delegation.SourceDocument;
        var intent = new MirrorAlertIntent(
            delegation.Id,
            source.CreatedById,
            source.BranchId ?? target.BranchId ?? 0,
            target.CreatedById,
            ReverseAdditionsMessage(result));
        await FireDelegationAlertAsync(intent, ct);
    }

    private static string ReverseAdditionsMessage(TargetEditResult result)
    {
        var parts = new List<string>();
        foreach (var h in result.AddedHeirs)
        {
            var name = string.Join(' ', new[] { h.HeirName, h.HeirFather, h.HeirFamily }.Where(v => !string.IsNullOrWhiteSpace(v)));
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var suffix = h.GuarantorNumber is { } num ? $" لكفيل-{num}" : string.Empty;
            parts.Add($"أُضيف الوريث {name}{suffix}");
        }
        parts.AddRange(result.NewRepresentativeLabels.Select(label => $"أُضيف {label}"));
        return $"تم تعديل بيانات الملف المناب بإضافة محلية — {string.Join("؛ ", parts)}";
    }
}