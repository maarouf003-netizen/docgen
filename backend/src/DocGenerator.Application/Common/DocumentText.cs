using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// النصوص والحراس الخالصة المشتركة لخدمات الملفات (المرحلة 1 — البيت المشترك):
/// بلا حالة ولا تبعيات. تُستهلك من النواة والخدمات المستخرجة (إجراءات/بحث/حالات/إنابة)
/// بدل تكرار المنطق — أي حاجة عكسية تُحل بنقل المساعد لا بنداء خدمة.
/// </summary>
public static class DocumentText
{
    /// <summary>
    /// الاسم الثلاثي للمنفذ عليه من بيانات المستند، يُلحق بكل أحداث التدقيق
    /// ليُعرف أي ملف وقع عليه الإجراء دون فتح صفحة المستند.
    /// </summary>
    public static string ActorFullName(Document doc) => string.Join(' ',
        new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(v => !string.IsNullOrWhiteSpace(v)));

    public static string AuditWithActor(string action, Document doc) =>
        string.IsNullOrWhiteSpace(ActorFullName(doc))
            ? action
            : $"{action} — المنفذ عليه: {ActorFullName(doc)}";

    public static string TypeLabel(string type) => type == "note" ? "ملاحظة" : "إجراء";

    /// <summary>
    /// اسم العرض للملف في صفحة التدوير. لعائلتي وضع «منفذ عليه» (Executed + Deposit) لا يوجد
    /// مقترض، فأُعرض اسم طالب العرض (الجهة العامة أو الشخص الطبيعي) كما تُعرض في ملفات الإكسل
    /// والطباعة، ولو تعذّر توافره فلا أسمًا. ولنظام «طالبة تنفيذ» تُستخدم أسماء المقترض المعتادة.
    /// </summary>
    public static string RotationDisplayName(Document d)
    {
        if (!GeneralEntitySideCatalog.IsExecutedLike(d.GeneralEntitySide))
            return FormatName(d.BorrowerName, d.BorrowerFather, d.BorrowerFamily);

        var applicant = d.ExecutionApplicants
            .Select(a => FormatName(a.Name, a.Father, a.Family))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(applicant))
            return applicant;

        var entity = d.ExecutedPublicEntities
            .Select(e => e.EntityName)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(entity))
            return entity;

        return d.ExecutedNaturalPersons
            .Select(p => FormatName(p.Name, p.Father, p.Family))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    public static string FormatName(string? first, string? father, string? family) =>
        string.Join(' ', new[] { first, father, family }.Where(v => !string.IsNullOrWhiteSpace(v)));

    public static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? DateText(DateTime? value) =>
        value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public static void ApplyCircuitDerivation(Document doc, ExecutionCircuit? circuit, DocumentUpsertRequest request)
    {
        if (circuit is not null)
        {
            doc.ExecutionCircuitId = circuit.Id;
            doc.Court = circuit.Name;
            doc.CourtNorm = ArabicNameNormalizer.Normalize(circuit.Name);
        }
        else
        {
            // انتقالي (فرع بلا سجل): إبقاء النص الحر المرسل — يُتجاهل فقط عند وجود دائرة.
            doc.ExecutionCircuitId = request.ExecutionCircuitId;
            doc.Court = request.Court;
        }
    }

    /// <summary>حارس المعلق الصريح: تُمنع عليه العمليات التي تلزم الهوية الرقمية.</summary>
    public static void EnsureNotPendingRegistration(Document doc, string actionLabel)
    {
        if (doc.NeedsRegistration)
            throw new ArgumentException($"لا يمكن {actionLabel} على ملف بانتظار إعادة القيد — أدخل رقمه الجديد أولًا");
    }

    /// <summary>
    /// بناء كفيل من صفّ الطلب بتطبيع الحالة: تُصفَّر حقول الهوية الطبيعية للشخص الاعتباري
    /// ويُحتفظ برقم التسجيل ومن يمثلها، والممثل الشرعي يُخزَّن للحقوق الطبيعية فقط (بصفته
    /// وعنوانه المضبوطين) ويُصفَّر عند الغياب — مشترك بين الحفظ والحارس (مقارنات المرآة).
    /// </summary>
    public static Guarantor BuildGuarantor(GuarantorDto g)
    {
        var nature = NormalizePartyNature(g.Nature);
        var isLegalGuarantor = PartyNatureCatalog.IsLegal(nature);
        var hasRep = !isLegalGuarantor
            && !IsEmptyRepresentative(g.RepresentativeName, g.RepresentativeFather, g.RepresentativeFamily);
        return new Guarantor
        {
            GuarantorNumber = g.GuarantorNumber,
            GuarantorName = g.Name,
            GuarantorFather = isLegalGuarantor ? null : g.Father,
            GuarantorFamily = isLegalGuarantor ? null : g.Family,
            GuarantorMother = isLegalGuarantor ? null : g.Mother,
            GuarantorBirth = isLegalGuarantor ? null : g.Birth,
            GuarantorRegister = isLegalGuarantor ? null : g.Register,
            GuarantorNationalId = isLegalGuarantor ? null : g.NationalId,
            GuarantorAddress = g.Address,
            AddressType = g.AddressType,
            GuarantorNature = nature,
            GuarantorRegistrationNumber = isLegalGuarantor ? (g.RegistrationNumber ?? string.Empty).Trim() : null,
            GuarantorRepresentedBy = isLegalGuarantor ? (g.RepresentedBy ?? string.Empty).Trim() : null,
            RepresentativeName = hasRep ? (g.RepresentativeName ?? string.Empty).Trim() : null,
            RepresentativeFather = hasRep ? (g.RepresentativeFather ?? string.Empty).Trim() : null,
            RepresentativeFamily = hasRep ? (g.RepresentativeFamily ?? string.Empty).Trim() : null,
            RepresentativeCapacity = hasRep ? NormalizeRepresentativeCapacity(g.RepresentativeCapacity) : null,
            RepresentativeAddressType = hasRep ? NormalizeRepresentativeAddressType(g.RepresentativeAddressType) : null,
            RepresentativeAddress = hasRep ? (g.RepresentativeAddress ?? string.Empty).Trim() : null,
        };
    }

    /// <summary>
    /// بناء أصل من صفّ الطلب بتطبيق قواعد النوع والحصص والتواريخ الحرة
    /// (مثل ApplyRequest تمامًا) — مشترك بين الحفظ والحارس (الفرق القيمي للأصول).
    /// </summary>
    public static Asset BuildAsset(AssetDto re)
    {
        var kind = (re.AssetKind ?? string.Empty).Trim();
        if (!AssetKindCatalog.IsValid(kind))
            throw new ArgumentException($"نوع الأصل غير صالح: {kind}");

        var asset = new Asset
        {
            AssetKind = kind,
            ShareType = re.ShareType,
            Property = re.Property,
            PropertyNumber = re.PropertyNumber,
            PropertyDistrict = re.PropertyDistrict,
            LandRegistry = re.LandRegistry,
            VehicleType = re.VehicleType,
            VehicleClass = re.VehicleClass,
            PlateNumber = re.PlateNumber,
            VehicleGovernorate = re.VehicleGovernorate,
            RegisterNumber = re.RegisterNumber,
            RegistrationDate = DocumentValidator.ParseDateTime(re.RegistrationDate, "تاريخ تسجيل المتجر"),
            ShopGovernorate = re.ShopGovernorate,
            ShopDescription = re.ShopDescription,
            ShopLocation = re.ShopLocation,
            PublicEntity = re.PublicEntity,
            LicenseNumber = re.LicenseNumber,
            LicenseDate = DocumentValidator.ParseDateTime(re.LicenseDate, "تاريخ الترخيص"),
            LicenseIssuer = re.LicenseIssuer,
            Notes = re.Notes,
            SeizureDate = DocumentValidator.ParseDateTime(re.SeizureDate, "تاريخ القاء الحجز"),
        };
        asset.Owners = AssetMapper.NormalizeOwners(re.Owners);
        // تمام الأصل لا يكون إلا لمالك واحد؛ عند تعدد الملاك تُفرض الحصة السهمية
        // حتى لو أُرسل نوع حصة آخر (حماية البيانات على مستوى الخدمة).
        // الأنواع غير الحصصية (كفالة الرواتب والمتجر غير المسجل) لا تحمل مقدار حصة.
        if (AssetKindCatalog.HasShare(kind))
        {
            if (asset.Owners.Count > 1)
                asset.ShareType = "حصة سهمية";
            else if (string.IsNullOrWhiteSpace(asset.ShareType))
                asset.ShareType = AssetKindCatalog.FullShareLabel(kind);
        }
        else
        {
            asset.ShareType = null;
        }
        return asset;
    }

    /// <summary>
    /// تطبيع قائمة الورثة: يُتجاهل من فرغ اسمه الثلاثي، وتُقصّ الحقول من الطرفين،
    /// ويُقيَّد نوع العنوان والصفة بالقيم المسموح بها.
    /// </summary>
    public static List<Heir> NormalizeHeirs(IEnumerable<HeirDto>? heirs, int? guarantorNumber)
    {
        var result = new List<Heir>();
        if (heirs is null)
            return result;

        foreach (var h in heirs)
        {
            var name = (h.Name ?? string.Empty).Trim();
            var father = (h.Father ?? string.Empty).Trim();
            var family = (h.Family ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(father) && string.IsNullOrWhiteSpace(family))
                continue;

            var addressType = (h.AddressType ?? string.Empty).Trim();
            if (addressType != "عنوان" && addressType != "وكيل" && addressType != "موطن مختار")
                addressType = "عنوان";

            var capacity = (h.Capacity ?? string.Empty).Trim();
            if (capacity != "إضافة لتركة" && capacity != "أصالة وإضافة")
                capacity = "أصالة";

            result.Add(new Heir
            {
                GuarantorNumber = guarantorNumber,
                HeirName = name,
                HeirFather = father,
                HeirFamily = family,
                HeirCapacity = capacity,
                AddressType = addressType,
                HeirAddress = (h.Address ?? string.Empty).Trim(),
            });
        }

        return result;
    }

    /// <summary>
    /// تطبيع قائمة الجهات طالبة التنفيذ في وضع «طالبة تنفيذ»: يُتجاهل ما بلا اسم جهة،
    /// ويُقصّ اسم الجهة وفرعها ومحافظتها.
    /// </summary>
    public static List<ApplicantPublicEntity> NormalizeApplicantPublicEntities(IEnumerable<ApplicantPublicEntityDto>? entities)
    {
        var result = new List<ApplicantPublicEntity>();
        if (entities is null)
            return result;

        foreach (var e in entities)
        {
            var name = (e.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result.Add(new ApplicantPublicEntity
            {
                Name = name,
                Branch = (e.Branch ?? string.Empty).Trim(),
                Governorate = (e.Governorate ?? string.Empty).Trim(),
                RegistryId = e.RegistryId,
            });
        }

        return result;
    }

    /// <summary>
    /// هل الممثل الشرعي غائب (اسمه الثلاثي فارغ كاملًا)؟ تُعدّ الحقول فارغة فلا يُخزَّن ممثل.
    /// </summary>
    public static bool IsEmptyRepresentative(string? name, string? father, string? family) =>
        string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(father) && string.IsNullOrWhiteSpace(family);

    /// <summary>
    /// صفة الممثل الشرعي المقبولة: ولي / وصي / قيم؛ أي قيمة أخرى أو فارغة تُعاد فارغة.
    /// </summary>
    public static string NormalizeRepresentativeCapacity(string? capacity)
    {
        var value = (capacity ?? string.Empty).Trim();
        return value is "ولي" or "وصي" or "قيم" ? value : string.Empty;
    }

    /// <summary>
    /// نوع عنوان الممثل الشرعي المقبول: موطن مختار / عنوان / وكيل قانوني؛ أي قيمة أخرى أو فارغة
    /// تُعيَّر إلى «عنوان».
    /// </summary>
    public static string NormalizeRepresentativeAddressType(string? addressType)
    {
        var value = (addressType ?? string.Empty).Trim();
        return value is "موطن مختار" or "عنوان" or "وكيل قانوني" ? value : "عنوان";
    }

    /// <summary>
    /// طبيعة الطرف المقبولة (مقترض/كفيل/طالب تنفيذ): شخص طبيعي (natural) أو شخص اعتباري (legal)؛
    /// أي قيمة أخرى أو فارغة تُعيَّر إلى «شخص طبيعي».
    /// </summary>
    public static string NormalizePartyNature(string? nature)
    {
        var value = (nature ?? string.Empty).Trim();
        return PartyNatureCatalog.ValidNatures.Contains(value) ? value : PartyNatureCatalog.Natural;
    }

    public static void FillDerivedFields(Document doc)
    {
        if (doc.AmountNumeric > 0 && string.IsNullOrWhiteSpace(doc.AmountWords))
            doc.AmountWords = FormatAmountWords(doc.AmountNumeric, doc.Currency);
        if (doc.Amount2Numeric > 0 && string.IsNullOrWhiteSpace(doc.Amount2Words))
            doc.Amount2Words = FormatAmountWords(doc.Amount2Numeric, doc.Currency2);
        if (doc.Amount3Numeric > 0 && string.IsNullOrWhiteSpace(doc.Amount3Words))
            doc.Amount3Words = FormatAmountWords(doc.Amount3Numeric, doc.Currency3);
        if (doc.InclusionAmountNumeric > 0 && string.IsNullOrWhiteSpace(doc.InclusionAmountWords))
            doc.InclusionAmountWords = FormatAmountWords(doc.InclusionAmountNumeric, doc.InclusionCurrency);
        if (doc.InclusionAmount2Numeric > 0 && string.IsNullOrWhiteSpace(doc.InclusionAmount2Words))
            doc.InclusionAmount2Words = FormatAmountWords(doc.InclusionAmount2Numeric, doc.InclusionCurrency2);
        if (doc.InclusionAmount3Numeric > 0 && string.IsNullOrWhiteSpace(doc.InclusionAmount3Words))
            doc.InclusionAmount3Words = FormatAmountWords(doc.InclusionAmount3Numeric, doc.InclusionCurrency3);

        // فك ارتباط IsDraft عن التفريغ: IsDraft = (فارغ الرقم/السنة) && !NeedsRegistration —
        // والمعلق يبقى IsDraft=false بشارة مستقلة (وإلا تلوث: منع الإنابة/الاستئناف على المسودات،
        // عدادات Drafts/Active، لافتة «تحت رفع»، قوائم المتداول، تصنيف البوابة).
        doc.IsDraft = (string.IsNullOrWhiteSpace(doc.FileNumber) || string.IsNullOrWhiteSpace(doc.FileYear))
            && !doc.NeedsRegistration;
        var label = doc.IsDraft ? ExecutionStatusCatalog.DraftFilter : "متداول";
        var borrower = (doc.BorrowerName ?? string.Empty).Trim();
        doc.DocumentType = string.IsNullOrWhiteSpace(borrower) ? label : $"{label} - {borrower}";

        // «طالب التنفيذ» في وضع «طالبة تنفيذ» يُشتق من قائمة الجهات (اسم + فرع بين قوسين)،
        // فتُوحَّد طريقة التخزين ويبقى النص متوافقًا مع البحث والتصدير والتوليد. وإن كانت
        // القائمة فارغة مع وجود نص قديم محفوظ يُحافظ عليه (توافق مع الطلبات القديمة).
        var applicantText = BuildApplicantText(doc.ApplicantPublicEntities);
        if (!string.IsNullOrWhiteSpace(applicantText) || string.IsNullOrWhiteSpace(doc.Applicant))
            doc.Applicant = applicantText;

        // نسخة تسريع لفلترة جهة الطالب في البوابة: اشتقاق موحّد من صفوف الجهات نفسها
        // (أول ربط سجلي غير فارغ)، وتُصفَّر تلقائيًا حين تفرغ القائمة أو يزول الربط.
        doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);

        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            // ملف «منفذ عليه»/«عرض وايداع»: مقيد دائمًا، والعنوان يعتمد على حالة الوضع،
            // واسم البحث يضم أسماء طلبات التنفيذ/العرض والجهات/الأشخاص المنفذ عليهم.
            doc.IsDraft = false;
            doc.DocumentType = $"{ExecutedStatusCatalog.ToLabel(doc.ExecutedStatus ?? ExecutedStatusCatalog.None)}";
        }

        // `PB-001`: الدائرة المعيارية لوحدانية الترقيم الحقيقية (تُقارَن معيارية-لمعيارية
        // في `ExistsActiveWithNumberAsync`؛ العرض يبقى `Court` الخام).
        doc.CourtNorm = string.IsNullOrWhiteSpace(doc.Court)
            ? null
            : ArabicNameNormalizer.Normalize(doc.Court);

        doc.SearchText = DocumentSearchTextBuilder.Build(doc);

        doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
    }

    private static string FormatAmountWords(decimal amount, string? currency)
    {
        // `NumberToWords` ساكن في `Services` تاريخيًا — يُستدعى مؤهلًا بلا نقل (خارج نطاق التفكيك).
        var words = Services.NumberToWords.Convert((long)amount);
        return string.IsNullOrWhiteSpace(words)
            ? string.Empty
            : $"{words} {currency} فقط لا غير".Trim();
    }

    /// <summary>
    /// النص الموحّد لطالب التنفيذ في وضع «طالبة تنفيذ» من قائمة الجهات:
    /// «الجهة - محافظة X و الجهة - محافظة Y» — يُشتق ليغذي البحث والتصدير والتوليد.
    /// الفرع لا يُضمّن هنا؛ يُعرض ويُفلتر عبر حقل الفرع المستقل في ApplicantPublicEntities.Branch.
    /// </summary>
    private static string BuildApplicantText(IEnumerable<ApplicantPublicEntity> entities) =>
        ApplicantTextBuilder.Build(entities);
}
