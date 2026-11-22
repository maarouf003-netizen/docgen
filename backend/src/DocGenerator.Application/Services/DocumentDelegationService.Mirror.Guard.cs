using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentDelegationService
{
    private static void ValidateMirrorGuarantors(
        TargetEditResult result, List<string> errors, IEnumerable<GuarantorDto> requestedDtos, Document doc)
    {
        var requested = requestedDtos.Select(DocumentText.BuildGuarantor).OrderBy(g => g.GuarantorNumber).ToList();
        var stored = doc.Guarantors.OrderBy(g => g.GuarantorNumber).ToList();
        var requestedNumbers = requested.Select(g => g.GuarantorNumber).ToList();
        var storedNumbers = stored.Select(g => g.GuarantorNumber).ToList();

        // رقم الكفيل مفتاح هوية للدمج والمقارنة (لا قيد فريد على (DocumentId, GuarantorNumber)
        // في القاعدة): تكراره يجعل المقارنة والدمج غير معرَّفين ويُفشل ToDictionary برسالة تقنية
        // إنكليزية مسرَّبة بدل رسالة الرفض العربية المتعاقد عليها — فيُرفض الطلب برسالة تحقق
        // صريحة قبل بناء المطابقات.
        if (requestedNumbers.Count != requestedNumbers.Distinct().Count())
        {
            errors.Add("أرقام الكفلاء مكررة — يجب أن يكون رقم كل كفيل فريدًا على الملف المناب");
            return;
        }
        if (storedNumbers.Count != storedNumbers.Distinct().Count())
        {
            errors.Add("أرقام الكفلاء المخزَّنة مكررة على الملف المناب — يلزم تصحيح البيانات قبل التعديل");
            return;
        }

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
            var requestedHeirs = DocumentText.NormalizeHeirs(dto.Heirs, g.GuarantorNumber);
            // تُقرأ حقول الممثل من صفّ الطلب مباشرةً لا من الكيان المُبنى (BuildGuarantor يُصفّر
            // ممثل الاعتباري مثل ApplyRequest) وإلا تعذّر على الحارس رؤية ممثلٍ على كفيل اعتباري
            // فيمر بصمت بدل الرفض الصريح (B3).
            var gHasRep = !DocumentText.IsEmptyRepresentative(dto.RepresentativeName, dto.RepresentativeFather, dto.RepresentativeFamily);
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
            var currentHasRep = !DocumentText.IsEmptyRepresentative(current.RepresentativeName, current.RepresentativeFather, current.RepresentativeFamily);
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
            var storedGuarantorHeirs = doc.Heirs.Where(h => h.GuarantorNumber == g.GuarantorNumber).ToList();
            var guarantorLabel = GuarantorFullName(g);
            ValidateMirrorHeirs(result, errors, requestedHeirs, storedGuarantorHeirs, gHasRep,
                new MirrorHeirLabels(
                    $"لا يمكن حذف ورثة الكفيل {guarantorLabel} من الملف المناب",
                    $"لا يمكن تعديل ورثة الكفيل {guarantorLabel} على الملف المناب"));
        }
    }

    private sealed record MirrorHeirLabels(string DeleteMessage, string EditMessage);

    /// <summary>
    /// حارس ورثة الطرف على الملف المناب (مشترك بين المقترض والكفيل): إضافة الوريث الناقص
    /// مسموحة، وحذف/تعديل القائم مرفوض، وممثل الوريث يُعبَّأ أول مرة فقط ويُبلَّغ كإضافة محلية.
    /// </summary>
    private static void ValidateMirrorHeirs(
        TargetEditResult result, List<string> errors,
        IReadOnlyList<Heir> requested, IReadOnlyList<Heir> stored,
        bool exemptAddress, MirrorHeirLabels labels)
    {
        var storedKeys = stored.Select(HeirIdentityKey).ToHashSet();
        var requestedKeys = requested.Select(HeirIdentityKey).ToHashSet();

        // وريث جديد (هوية غير موجودة): إضافة محلية مسموحة تُخطر المنيب، ومعها ممثله إن وُجد.
        foreach (var h in requested)
        {
            if (storedKeys.Contains(HeirIdentityKey(h)))
                continue;
            result.AddedHeirs.Add(new AddedHeirEntry(HeirDisplayName(h), h.GuarantorNumber));
            if (HasHeirRepresentative(h))
                result.NewRepresentativeLabels.Add($"الممثل الشرعي للوريث {HeirDisplayName(h)}");
        }

        // حذف وريث قائم: مرفوض.
        foreach (var h in stored)
        {
            if (!requestedKeys.Contains(HeirIdentityKey(h)))
            {
                errors.Add(labels.DeleteMessage);
                break;
            }
        }

        // تعديل وريث قائم أو ممثله: الصفة/العنوان مرفوضان، وممثل الوريث تعبئة أول مرة فقط.
        foreach (var h in requested)
        {
            var match = stored.FirstOrDefault(s => HeirIdentityKey(s) == HeirIdentityKey(h));
            if (match is null)
                continue;

            if (!HeirValueEquals(match, h, exemptAddress))
            {
                errors.Add(labels.EditMessage);
                break;
            }

            var storedRep = HasHeirRepresentative(match);
            var requestedRep = HasHeirRepresentative(h);
            if (storedRep && !requestedRep)
            {
                errors.Add($"لا يمكن إزالة الممثل الشرعي للوريث {HeirDisplayName(match)} من الملف المناب");
                break;
            }
            if (storedRep && requestedRep && !HeirRepresentativeEquals(match, h))
            {
                errors.Add($"لا يمكن تعديل الممثل الشرعي للوريث {HeirDisplayName(match)} على الملف المناب");
                break;
            }
            if (!storedRep && requestedRep)
                result.NewRepresentativeLabels.Add($"الممثل الشرعي للوريث {HeirDisplayName(h)}");
        }
    }

    private static void RequireSame(List<string> errors, string label, string? requested, string? current)
    {
        if (!string.Equals(DocumentText.Trimmed(requested), DocumentText.Trimmed(current), StringComparison.Ordinal))
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
            DocumentText.Trimmed(h.HeirName) ?? string.Empty,
            DocumentText.Trimmed(h.HeirFather) ?? string.Empty,
            DocumentText.Trimmed(h.HeirFamily) ?? string.Empty);

    /// <summary>هل للوريث ممثل شرعي مخزَّن/مُرسَل (اسم ثلاثي غير فارغ)؟</summary>
    private static bool HasHeirRepresentative(Heir h) =>
        !DocumentText.IsEmptyRepresentative(h.RepresentativeName, h.RepresentativeFather, h.RepresentativeFamily);

    /// <summary>هل تطابقت حقول الممثل الشرعي للوريث (بعد التطبيع نفسه الذي يعتمده الحفظ)?</summary>
    private static bool HeirRepresentativeEquals(Heir a, Heir b) =>
        string.Equals(DocumentText.Trimmed(a.RepresentativeName), DocumentText.Trimmed(b.RepresentativeName), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.RepresentativeFather), DocumentText.Trimmed(b.RepresentativeFather), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.RepresentativeFamily), DocumentText.Trimmed(b.RepresentativeFamily), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.RepresentativeCapacity), DocumentText.Trimmed(b.RepresentativeCapacity), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.RepresentativeAddressType), DocumentText.Trimmed(b.RepresentativeAddressType), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.RepresentativeAddress), DocumentText.Trimmed(b.RepresentativeAddress), StringComparison.Ordinal);

    /// <summary>هل تطابق الوريث القيمي (الصفة والعنوان)؟ — لتأكيد بقائه كما هو.
    /// عند إعفاء العنوان (ورثة/ممثل على الطرف في الطلب) أو عند وجود ممثل للوريث نفسه
    /// يُستثنى العنوان/نوعه من المقارنة لأن عنوان الممثل هو المعتبر (والحفظ يصفّر عنوان الوريث).
    /// ممثل الوريث يُقارن منفردًا بقاعدة «تعبئة أول مرة فقط».</summary>
    private static bool HeirValueEquals(Heir a, Heir b, bool exemptAddress) =>
        string.Equals(DocumentText.Trimmed(a.HeirCapacity), DocumentText.Trimmed(b.HeirCapacity), StringComparison.Ordinal)
        && (exemptAddress
            || HasHeirRepresentative(a)
            || HasHeirRepresentative(b)
            || (string.Equals(DocumentText.Trimmed(a.AddressType), DocumentText.Trimmed(b.AddressType), StringComparison.Ordinal)
                && string.Equals(DocumentText.Trimmed(a.HeirAddress), DocumentText.Trimmed(b.HeirAddress), StringComparison.Ordinal)));

    /// <summary>هل تطابقت قيم الكفيل غير الممثل؟ (الدمج بالرقم المرجعي لا المساس بالحقول الممثلة).
    /// عند إعفاء العنوان (ورثة/ممثل على هذا الكفيل في الطلب) يُستثنى عنوانه ونوعه من المقارنة.</summary>
    private static bool GuarantorCoreEquals(Guarantor a, Guarantor b, bool exemptAddress = false) =>
        string.Equals(DocumentText.Trimmed(a.GuarantorName), DocumentText.Trimmed(b.GuarantorName), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.GuarantorFather), DocumentText.Trimmed(b.GuarantorFather), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.GuarantorFamily), DocumentText.Trimmed(b.GuarantorFamily), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.GuarantorMother), DocumentText.Trimmed(b.GuarantorMother), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.GuarantorBirth), DocumentText.Trimmed(b.GuarantorBirth), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.GuarantorRegister), DocumentText.Trimmed(b.GuarantorRegister), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.GuarantorNationalId), DocumentText.Trimmed(b.GuarantorNationalId), StringComparison.Ordinal)
        && (exemptAddress
            || (string.Equals(DocumentText.Trimmed(a.GuarantorAddress), DocumentText.Trimmed(b.GuarantorAddress), StringComparison.Ordinal)
                && string.Equals(DocumentText.Trimmed(a.AddressType), DocumentText.Trimmed(b.AddressType), StringComparison.Ordinal)))
        && string.Equals(DocumentText.Trimmed(a.GuarantorNature), DocumentText.Trimmed(b.GuarantorNature), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.GuarantorRegistrationNumber), DocumentText.Trimmed(b.GuarantorRegistrationNumber), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.GuarantorRepresentedBy), DocumentText.Trimmed(b.GuarantorRepresentedBy), StringComparison.Ordinal);

    /// <summary>هل تطابقت حقول الممثل الشرعي للكفيل المُطبَّعة (المخزَّنة مقابل المُرسَلة)?</summary>
    private static bool GuarantorRepresentativeEquals(Guarantor a, Guarantor b) =>
        string.Equals(DocumentText.Trimmed(a.RepresentativeName), DocumentText.Trimmed(b.RepresentativeName), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.RepresentativeFather), DocumentText.Trimmed(b.RepresentativeFather), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.RepresentativeFamily), DocumentText.Trimmed(b.RepresentativeFamily), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.RepresentativeCapacity), DocumentText.Trimmed(b.RepresentativeCapacity), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.RepresentativeAddressType), DocumentText.Trimmed(b.RepresentativeAddressType), StringComparison.Ordinal)
        && string.Equals(DocumentText.Trimmed(a.RepresentativeAddress), DocumentText.Trimmed(b.RepresentativeAddress), StringComparison.Ordinal);

    /// <summary>هل الممثل الشرعي للمقترض المخزَّن يطابق ما أُرسل (بعد التطبيع نفسه الذي يعتمده الحفظ)?</summary>
    private static bool BorrowerRepresentativeEquals(Document d, DocumentUpsertRequest r)
    {
        var requested = BorrowerRepresentativeRequestValues(r);
        return string.Equals(DocumentText.Trimmed(d.BorrowerRepresentativeName), requested.RepresentativeName, StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(d.BorrowerRepresentativeFather), requested.RepresentativeFather, StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(d.BorrowerRepresentativeFamily), requested.RepresentativeFamily, StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(d.BorrowerRepresentativeCapacity), requested.RepresentativeCapacity, StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(d.BorrowerRepresentativeAddressType), requested.RepresentativeAddressType, StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(d.BorrowerRepresentativeAddress), requested.RepresentativeAddress, StringComparison.Ordinal);
    }

    /// <summary>حقول الممثل الشرعي للمقترض في الطلب مُطبَّعة كما يخزّنها ApplyRequest (صفرة عند الغياب).</summary>
    private static (string? RepresentativeName, string? RepresentativeFather, string? RepresentativeFamily,
        string? RepresentativeCapacity, string? RepresentativeAddressType, string? RepresentativeAddress)
        BorrowerRepresentativeRequestValues(DocumentUpsertRequest r)
    {
        if (DocumentText.IsEmptyRepresentative(r.BorrowerRepresentativeName, r.BorrowerRepresentativeFather, r.BorrowerRepresentativeFamily))
            return (null, null, null, null, null, null);
        return (
            (r.BorrowerRepresentativeName ?? string.Empty).Trim(),
            (r.BorrowerRepresentativeFather ?? string.Empty).Trim(),
            (r.BorrowerRepresentativeFamily ?? string.Empty).Trim(),
            DocumentText.NormalizeRepresentativeCapacity(r.BorrowerRepresentativeCapacity),
            DocumentText.NormalizeRepresentativeAddressType(r.BorrowerRepresentativeAddressType),
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
        var sameName = string.Equals(DocumentText.Trimmed(a.Name), DocumentText.Trimmed(b.Name), StringComparison.Ordinal);
        if (!sameRegistry && !sameName)
            return false;
        return string.Equals(DocumentText.Trimmed(a.Branch), DocumentText.Trimmed(b.Branch), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.Governorate), DocumentText.Trimmed(b.Governorate), StringComparison.Ordinal);
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
        if (!string.Equals(DocumentText.Trimmed(a.AssetKind), DocumentText.Trimmed(b.AssetKind), StringComparison.Ordinal))
            return false;
        if (!string.Equals(DocumentText.Trimmed(a.ShareType), DocumentText.Trimmed(b.ShareType), StringComparison.Ordinal))
            return false;
        if (!SameOwners(a.Owners, b.Owners))
            return false;
        // نوع الحق: الفارغ القديم يُطابَق بالافتراضي المُطبَّع («حق رقبة») كي لا تُرفض
        // تعديلات الملفات المنابة القديمة بعد إدخال الحقل الجديد.
        var aRightType = RightTypeCatalog.Normalize(a.AssetKind, a.RightType);
        var bRightType = RightTypeCatalog.Normalize(b.AssetKind, b.RightType);
        return string.Equals(DocumentText.Trimmed(a.Property), DocumentText.Trimmed(b.Property), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.PropertyNumber), DocumentText.Trimmed(b.PropertyNumber), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.CaseNumber), DocumentText.Trimmed(b.CaseNumber), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.PropertyDistrict), DocumentText.Trimmed(b.PropertyDistrict), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.LandRegistry), DocumentText.Trimmed(b.LandRegistry), StringComparison.Ordinal)
            && string.Equals(aRightType, bRightType, StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.VehicleType), DocumentText.Trimmed(b.VehicleType), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.VehicleClass), DocumentText.Trimmed(b.VehicleClass), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.PlateNumber), DocumentText.Trimmed(b.PlateNumber), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.VehicleGovernorate), DocumentText.Trimmed(b.VehicleGovernorate), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.RegisterNumber), DocumentText.Trimmed(b.RegisterNumber), StringComparison.Ordinal)
            && a.RegistrationDate == b.RegistrationDate
            && string.Equals(DocumentText.Trimmed(a.ShopGovernorate), DocumentText.Trimmed(b.ShopGovernorate), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.ShopDescription), DocumentText.Trimmed(b.ShopDescription), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.ShopLocation), DocumentText.Trimmed(b.ShopLocation), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.PublicEntity), DocumentText.Trimmed(b.PublicEntity), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.LicenseNumber), DocumentText.Trimmed(b.LicenseNumber), StringComparison.Ordinal)
            && a.LicenseDate == b.LicenseDate
            && string.Equals(DocumentText.Trimmed(a.LicenseIssuer), DocumentText.Trimmed(b.LicenseIssuer), StringComparison.Ordinal)
            && string.Equals(DocumentText.Trimmed(a.Notes), DocumentText.Trimmed(b.Notes), StringComparison.Ordinal)
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
            if (!string.Equals(DocumentText.Trimmed(anames[i]), DocumentText.Trimmed(bnames[i]), StringComparison.Ordinal))
                return false;
        }
        return true;
    }
}
