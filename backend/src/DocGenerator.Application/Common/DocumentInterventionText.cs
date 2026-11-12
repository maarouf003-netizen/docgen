using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// تطبيع وحقول «المتدخل»: المصدر الوحيد لتحويل طلب الإضافة/التعديل إلى كيان
/// (بتصفير حقول الصفات غير المعنية)، وتوليد اسم العرض، ووقعة «طلب تدخل» ومزامنتها.
/// </summary>
public static class DocumentInterventionText
{
    private const string DefaultPartyAddressType = "موطن مختار";

    /// <summary>
    /// يبني كيان متدخل من الطلب بعد كل التحققات: الصفة، إلزام الاسم وتاريخ الورود،
    /// عدم سبق القبول للورود، حدود الأطوال، وتطبيع العناوين والممثل والورثة.
    /// </summary>
    public static DocumentIntervention Normalize(DocumentInterventionUpsertRequest request)
    {
        var nature = (request.Nature ?? string.Empty).Trim().ToLowerInvariant();
        if (!PartyNatureCatalog.ValidNatures.Contains(nature)
            && nature != PartyNatureCatalog.PublicEntity)
            throw new ArgumentException("صفة المتدخل غير صالحة");

        var receiptDate = DocumentValidator.ParseDateTime(request.ReceiptDate, "تاريخ ورود طلب التدخل");
        if (receiptDate is null)
            throw new ArgumentException("تاريخ ورود طلب التدخل مطلوب");

        var acceptanceDate = DocumentValidator.ParseDateTime(request.AcceptanceDate, "تاريخ قبول طلب التدخل");
        // قاعدة العمل: قبول طلب التدخل يسبق ورود الطلب دائمًا (الورود هو تاريخ ورود
        // الطلب بعد القبول)، فلا يُقبل القبول في تاريخ الورود نفسه أو بعده.
        if (acceptanceDate is not null && acceptanceDate >= receiptDate)
            throw new ArgumentException("تاريخ قبول طلب التدخل يجب أن يسبق تاريخ الورود");

        var intervention = new DocumentIntervention
        {
            Nature = nature,
            ReceiptNumber = DigitNormalizer.NormalizeDigitsOrNull(
                Clean(request.ReceiptNumber, InterventionLimitsCatalog.MaxReceiptNumber, "رقم ورود طلب التدخل")),
            ReceiptDate = receiptDate,
            AcceptanceDate = acceptanceDate,
            Subject = Clean(request.Subject, InterventionLimitsCatalog.MaxSubject, "موضوع التدخل", multiline: true),
            Notes = Clean(request.Notes, InterventionLimitsCatalog.MaxNotes, "ملاحظات التدخل", multiline: true),
        };

        switch (nature)
        {
            case PartyNatureCatalog.Natural:
                ApplyNatural(intervention, request);
                break;
            case PartyNatureCatalog.Legal:
                ApplyLegal(intervention, request);
                break;
            default:
                ApplyPublicEntity(intervention, request);
                break;
        }

        return intervention;
    }

    /// <summary>نسخ حقول المتدخل المطبَّع إلى كيان قائم (تعديل) — بما فيه استبدال ورثته.</summary>
    public static void Apply(DocumentIntervention target, DocumentIntervention source)
    {
        target.Nature = source.Nature;
        target.ReceiptNumber = source.ReceiptNumber;
        target.ReceiptDate = source.ReceiptDate;
        target.AcceptanceDate = source.AcceptanceDate;
        target.Subject = source.Subject;
        target.Notes = source.Notes;

        target.Name = source.Name;
        target.Father = source.Father;
        target.Family = source.Family;
        target.Mother = source.Mother;
        target.Birth = source.Birth;
        target.Register = source.Register;
        target.NationalId = source.NationalId;
        target.AddressType = source.AddressType;
        target.Address = source.Address;

        target.RepresentativeName = source.RepresentativeName;
        target.RepresentativeFather = source.RepresentativeFather;
        target.RepresentativeFamily = source.RepresentativeFamily;
        target.RepresentativeCapacity = source.RepresentativeCapacity;
        target.RepresentativeAddressType = source.RepresentativeAddressType;
        target.RepresentativeAddress = source.RepresentativeAddress;

        target.EntityName = source.EntityName;
        target.EntityBranch = source.EntityBranch;
        target.RegistrationNumber = source.RegistrationNumber;
        target.RepresentedBy = source.RepresentedBy;
        target.Governorate = source.Governorate;
        target.RegistryId = source.RegistryId;

        target.Heirs.Clear();
        foreach (var heir in source.Heirs)
            target.Heirs.Add(heir);

        target.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>اسم العرض: الاسم الثلاثي للطبيعي، واسم الكيان للاعتباري، واسم الجهة وفرعها للعامة.</summary>
    public static string DisplayName(DocumentIntervention intervention) => intervention.Nature switch
    {
        PartyNatureCatalog.Legal => (intervention.EntityName ?? string.Empty).Trim(),
        PartyNatureCatalog.PublicEntity => JoinNonEmpty(
            intervention.EntityName, intervention.EntityBranch),
        _ => JoinNonEmpty(intervention.Name, intervention.Father, intervention.Family),
    };

    /// <summary>تحويل الكيان إلى عقد الاستجابة (التواريخ نصًا `yyyy-MM-dd`).</summary>
    public static DocumentInterventionDto ToDto(DocumentIntervention intervention) => new(
        intervention.Id,
        intervention.Nature,
        PartyNatureCatalog.ToLabel(intervention.Nature),
        DisplayName(intervention),
        intervention.ReceiptNumber,
        FormatDate(intervention.ReceiptDate),
        FormatDate(intervention.AcceptanceDate),
        intervention.Subject,
        intervention.Notes,
        intervention.Name,
        intervention.Father,
        intervention.Family,
        intervention.Mother,
        intervention.Birth,
        intervention.Register,
        intervention.NationalId,
        intervention.AddressType,
        intervention.Address,
        intervention.RepresentativeName,
        intervention.RepresentativeFather,
        intervention.RepresentativeFamily,
        intervention.RepresentativeCapacity,
        intervention.RepresentativeAddressType,
        intervention.RepresentativeAddress,
        intervention.EntityName,
        intervention.EntityBranch,
        intervention.RegistrationNumber,
        intervention.RepresentedBy,
        intervention.Governorate,
        intervention.RegistryId,
        intervention.Heirs
            .OrderBy(h => h.Id)
            .Select(ToHeirDto)
            .ToList());

    /// <summary>وقعة «طلب تدخل» جديدة مرتبطة بالمتدخل (تاريخ الوقعة = تاريخ الورود).</summary>
    public static DocumentOccurrence BuildOccurrence(
        DocumentIntervention intervention, int documentId, int userId) => new()
        {
            DocumentId = documentId,
            OccurrenceType = OccurrenceTypeCatalog.Intervention,
            Source = OccurrenceSourceCatalog.System,
            EventDate = intervention.ReceiptDate,
            DocumentIntervention = intervention,
            Details = BuildOccurrenceDetails(intervention),
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    /// <summary>مزامنة لقطة الوقعة (الاسم والقبول وتاريخ الحدث) مع بيانات المتدخل بعد التعديل.</summary>
    public static void ApplyOccurrence(DocumentOccurrence occurrence, DocumentIntervention intervention)
    {
        occurrence.EventDate = intervention.ReceiptDate;
        occurrence.Details = BuildOccurrenceDetails(intervention);
        occurrence.UpdatedAt = DateTime.UtcNow;
    }

    private static string BuildOccurrenceDetails(DocumentIntervention intervention)
    {
        var details = new Dictionary<string, string>
        {
            ["intervenorName"] = DisplayName(intervention),
            ["nature"] = intervention.Nature,
        };
        if (intervention.AcceptanceDate is not null)
            details["acceptanceDate"] = FormatDate(intervention.AcceptanceDate)!;
        return JsonSerializer.Serialize(details);
    }

    private static void ApplyNatural(DocumentIntervention intervention, DocumentInterventionUpsertRequest request)
    {
        intervention.Name = Require(
            Clean(request.Name, InterventionLimitsCatalog.MaxName, "اسم المتدخل"), "اسم المتدخل مطلوب");
        intervention.Father = Clean(request.Father, InterventionLimitsCatalog.MaxName, "اسم أب المتدخل");
        intervention.Family = Clean(request.Family, InterventionLimitsCatalog.MaxName, "نسبة المتدخل");
        intervention.Mother = Clean(request.Mother, InterventionLimitsCatalog.MaxName, "اسم أم المتدخل");
        intervention.Birth = Clean(request.Birth, InterventionLimitsCatalog.MaxBirth, "مكان وتاريخ ولادة المتدخل");
        intervention.Register = Clean(request.Register, InterventionLimitsCatalog.MaxRegister, "مكان ورقم قيد المتدخل");
        intervention.NationalId = Clean(request.NationalId, InterventionLimitsCatalog.MaxNationalId, "الرقم الوطني للمتدخل");

        var heirs = NormalizeHeirs(request.Heirs);
        var hasRepresentative = !DocumentText.IsEmptyRepresentative(
            request.RepresentativeName, request.RepresentativeFather, request.RepresentativeFamily);
        if (hasRepresentative && heirs.Count > 0)
            throw new ArgumentException("لا يجتمع الممثل الشرعي مع ورثة المتدخل");

        // عنوان المتدخل أو وكيله لا يُطلب إذا وُجد ممثل شرعي (عنوان الممثل هو المعتبر)
        // أو ورثة (المتدخل متوفى). تُصفَّر الحقول صراحةً حتى لا تبقى بيانات قديمة في التعديل.
        var suppressAddress = hasRepresentative || heirs.Count > 0;
        intervention.AddressType = suppressAddress ? null : NormalizePartyAddressType(request.AddressType);
        intervention.Address = suppressAddress
            ? null
            : Clean(request.Address, InterventionLimitsCatalog.MaxAddress, "عنوان المتدخل أو وكيله");

        if (hasRepresentative)
        {
            intervention.RepresentativeName = Require(
                Clean(request.RepresentativeName, InterventionLimitsCatalog.MaxName, "اسم الممثل الشرعي"),
                "اسم الممثل الشرعي مطلوب عند إضافة ممثل");
            intervention.RepresentativeFather = Clean(
                request.RepresentativeFather, InterventionLimitsCatalog.MaxName, "اسم أب الممثل الشرعي");
            intervention.RepresentativeFamily = Clean(
                request.RepresentativeFamily, InterventionLimitsCatalog.MaxName, "نسبة الممثل الشرعي");
            intervention.RepresentativeCapacity = DocumentText.NormalizeRepresentativeCapacity(
                request.RepresentativeCapacity);
            intervention.RepresentativeAddressType = DocumentText.NormalizeRepresentativeAddressType(
                request.RepresentativeAddressType);
            intervention.RepresentativeAddress = Clean(
                request.RepresentativeAddress, InterventionLimitsCatalog.MaxAddress, "عنوان الممثل الشرعي أو وكيله");
        }

        foreach (var heir in heirs)
            intervention.Heirs.Add(heir);
    }

    private static void ApplyLegal(DocumentIntervention intervention, DocumentInterventionUpsertRequest request)
    {
        intervention.EntityName = Require(
            Clean(request.EntityName, InterventionLimitsCatalog.MaxEntityName, "اسم الشخص الاعتباري"),
            "اسم الشخص الاعتباري مطلوب");
        intervention.RegistrationNumber = Clean(
            request.RegistrationNumber, InterventionLimitsCatalog.MaxRegistrationNumber, "رقم تسجيل الشخص الاعتباري");
        intervention.RepresentedBy = Clean(
            request.RepresentedBy, InterventionLimitsCatalog.MaxRepresentedBy, "من يمثل الشخص الاعتباري");
        intervention.Governorate = Clean(
            request.Governorate, InterventionLimitsCatalog.MaxGovernorate, "محافظة الشخص الاعتباري");
        intervention.AddressType = NormalizePartyAddressType(request.AddressType);
        intervention.Address = Clean(request.Address, InterventionLimitsCatalog.MaxAddress, "عنوان الشخص الاعتباري أو وكيله");
    }

    private static void ApplyPublicEntity(DocumentIntervention intervention, DocumentInterventionUpsertRequest request)
    {
        if (request.RegistryId is null)
            throw new ArgumentException("اختر جهة عامة صالحة من السجل المرجعي");

        // الاسم/الفرع/المحافظة تُعتمد من السجل المرجعي في الخدمة؛ هنا نُبقي نص العميل
        // (اختياريًا) للتحقق من الطول فقط، فلا يصبح غيابه سبب رفض زائف.
        intervention.EntityName = Clean(
            request.EntityName, InterventionLimitsCatalog.MaxEntityName, "اسم الجهة العامة");
        intervention.EntityBranch = Clean(
            request.EntityBranch, InterventionLimitsCatalog.MaxEntityBranch, "فرع الجهة العامة");
        intervention.Governorate = Clean(
            request.Governorate, InterventionLimitsCatalog.MaxGovernorate, "محافظة الجهة العامة");
        intervention.RegistryId = request.RegistryId;
        // الجهة العامة لا تحمل عنوانًا/وكيلًا نصيًا في هذا السجل: تُصفَّر الحقول الموروثة
        // من الحالة الابتدائية بعكس الطبيعة الطبيعية/الاعتبارية.
        intervention.AddressType = null;
        intervention.Address = null;
    }

    private static List<DocumentInterventionHeir> NormalizeHeirs(IEnumerable<HeirDto>? heirs)
    {
        var result = new List<DocumentInterventionHeir>();
        if (heirs is null)
            return result;

        foreach (var h in heirs)
        {
            var name = Clean(h.Name, InterventionLimitsCatalog.MaxName, "اسم الوريث");
            var father = Clean(h.Father, InterventionLimitsCatalog.MaxName, "اسم أب الوريث");
            var family = Clean(h.Family, InterventionLimitsCatalog.MaxName, "نسبة الوريث");
            if (name is null && father is null && family is null)
                continue;

            var hasRepresentative = !DocumentText.IsEmptyRepresentative(
                h.RepresentativeName, h.RepresentativeFather, h.RepresentativeFamily);
            result.Add(new DocumentInterventionHeir
            {
                HeirName = name,
                HeirFather = father,
                HeirFamily = family,
                Capacity = DocumentText.NormalizeHeirCapacity(h.Capacity),
                AddressType = hasRepresentative ? null : DocumentText.NormalizeHeirAddressType(h.AddressType),
                HeirAddress = hasRepresentative
                    ? null
                    : Clean(h.Address, InterventionLimitsCatalog.MaxAddress, "عنوان الوريث أو وكيله"),
                RepresentativeName = hasRepresentative
                    ? Clean(h.RepresentativeName, InterventionLimitsCatalog.MaxName, "اسم الممثل الشرعي للوريث")
                    : null,
                RepresentativeFather = hasRepresentative
                    ? Clean(h.RepresentativeFather, InterventionLimitsCatalog.MaxName, "اسم أب الممثل الشرعي للوريث")
                    : null,
                RepresentativeFamily = hasRepresentative
                    ? Clean(h.RepresentativeFamily, InterventionLimitsCatalog.MaxName, "نسبة الممثل الشرعي للوريث")
                    : null,
                RepresentativeCapacity = hasRepresentative
                    ? DocumentText.NormalizeRepresentativeCapacity(h.RepresentativeCapacity)
                    : null,
                RepresentativeAddressType = hasRepresentative
                    ? DocumentText.NormalizeRepresentativeAddressType(h.RepresentativeAddressType)
                    : null,
                RepresentativeAddress = hasRepresentative
                    ? Clean(h.RepresentativeAddress, InterventionLimitsCatalog.MaxAddress, "عنوان الممثل الشرعي للوريث")
                    : null,
            });
        }

        return result;
    }

    private static HeirDto ToHeirDto(DocumentInterventionHeir h) => new(
        h.Id,
        h.HeirName,
        h.HeirFather,
        h.HeirFamily,
        h.Capacity,
        h.AddressType,
        h.HeirAddress,
        h.RepresentativeName,
        h.RepresentativeFather,
        h.RepresentativeFamily,
        h.RepresentativeCapacity,
        h.RepresentativeAddressType,
        h.RepresentativeAddress);

    private static string NormalizePartyAddressType(string? addressType)
    {
        var value = (addressType ?? string.Empty).Trim();
        return value is "موطن مختار" or "عنوان" or "يمثله" ? value : DefaultPartyAddressType;
    }

    private static string? Clean(string? value, int max, string label, bool multiline = false)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = multiline
            ? value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim()
            : value.Trim();
        if (normalized.Length > max)
            throw new ArgumentException($"{label} يتجاوز الحد الأقصى ({max} محرفًا)");
        return normalized;
    }

    private static string Require(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value;

    private static string JoinNonEmpty(params string?[] parts) =>
        string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static string? FormatDate(DateTime? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
