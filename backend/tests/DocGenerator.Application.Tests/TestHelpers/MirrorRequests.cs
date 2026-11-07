using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// بناة طلبات «تعديل الملف المناب» من لقطة المستند المخزَّنة: كل حقل يُعاد كما هو حتى لا
/// يرى حارس المرآة أي تغيير غير مقصود، ثم يُعدَّل ما يخصّ الاختبار (وريث/ممثل) فقط.
/// مشتركة بين اختبارات خدمة الإنابة واختبارات التزامن (نُقلت حرفيًا من
/// <c>DocumentDelegationServiceTests</c> بلا تغيير دلالي — بلا تكرار ثالث).
/// </summary>
internal static class MirrorRequests
{
    public static DocumentUpsertRequest MirrorRequest(Document doc) => new()
    {
        GeneralEntitySide = doc.GeneralEntitySide,
        DocumentType = doc.DocumentType,
        BorrowerName = doc.BorrowerName,
        BorrowerFather = doc.BorrowerFather,
        BorrowerFamily = doc.BorrowerFamily,
        BorrowerMother = doc.BorrowerMother,
        BorrowerBirth = doc.BorrowerBirth,
        BorrowerRegister = doc.BorrowerRegister,
        BorrowerNationalId = doc.BorrowerNationalId,
        BorrowerAddress = doc.BorrowerAddress,
        BorrowerAddressType = doc.BorrowerAddressType,
        BorrowerNature = doc.BorrowerNature,
        BorrowerRegistrationNumber = doc.BorrowerRegistrationNumber,
        BorrowerRepresentedBy = doc.BorrowerRepresentedBy,
        BorrowerRepresentativeName = doc.BorrowerRepresentativeName,
        BorrowerRepresentativeFather = doc.BorrowerRepresentativeFather,
        BorrowerRepresentativeFamily = doc.BorrowerRepresentativeFamily,
        BorrowerRepresentativeCapacity = doc.BorrowerRepresentativeCapacity,
        BorrowerRepresentativeAddressType = doc.BorrowerRepresentativeAddressType,
        BorrowerRepresentativeAddress = doc.BorrowerRepresentativeAddress,
        ContractType = doc.ContractType,
        ContractTypeSelector = doc.ContractTypeSelector,
        ContractNumber = doc.ContractNumber,
        ContractDate = doc.ContractDate,
        AnnexType = doc.AnnexType,
        AnnexNumber = doc.AnnexNumber,
        AnnexDate = doc.AnnexDate,
        Annexes = doc.Annexes
            .OrderBy(a => a.Sequence)
            .Select(a => new DocumentAnnexDto(a.Id, a.AnnexType, a.AnnexNumber, a.AnnexDate, a.Sequence))
            .ToList(),
        InclusionText = doc.InclusionText,
        InclusionNotes = doc.InclusionNotes,
        AmountNumeric = doc.AmountNumeric,
        // لا حقول «كتابة»: التفقيط مشتقّ خادميًا وغير موجود في عقد الطلب — وهذا هو
        // شكل الحمولة الحقيقي من الواجهة الذي يجب أن يقبله الحارس على المناب.
        Currency = doc.Currency,
        Amount2Numeric = doc.Amount2Numeric,
        Currency2 = doc.Currency2,
        Amount3Numeric = doc.Amount3Numeric,
        Currency3 = doc.Currency3,
        InclusionAmountNumeric = doc.InclusionAmountNumeric,
        InclusionCurrency = doc.InclusionCurrency,
        InclusionAmount2Numeric = doc.InclusionAmount2Numeric,
        InclusionCurrency2 = doc.InclusionCurrency2,
        InclusionAmount3Numeric = doc.InclusionAmount3Numeric,
        InclusionCurrency3 = doc.InclusionCurrency3,
        Court = doc.Court,
        Applicant = doc.Applicant,
        FileNumber = doc.FileNumber,
        FileType = doc.FileType,
        FileYear = doc.FileYear,
        FileRegistrationDate = doc.RegistrationDate?.Date ?? "1/1/2024",
        FileIncoming = doc.FileIncoming,
        FileIncomingDate = doc.FileIncomingDate,
        UnderFilingNumber = doc.UnderFilingNumber,
        BranchName = doc.BranchName,
        SeizureDate = doc.SeizureDate,
        ImmediateActions = doc.ImmediateActions,
        Notes = doc.Notes,
        FileArrivalNumber = doc.FileArrivalNumber,
        FileArrivalDate = doc.FileArrivalDate,
        FileReceiptNumber = doc.FileReceiptNumber,
        FileReceiptDate = doc.FileReceiptDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ApplicantPublicEntities = new List<ApplicantPublicEntityDto>(),
        ExecutionApplicants = new List<ExecutionApplicantDto>(),
        ExecutedPublicEntities = new List<ExecutedPublicEntityDto>(),
        ExecutedNaturalPersons = new List<ExecutedNaturalPersonDto>(),
        Guarantors = new List<GuarantorDto>(),
        BorrowerHeirs = new List<HeirDto>(),
        Assets = new List<AssetDto>(),
    };

    /// <summary>صفّ كفلاء المستند كامِلًا (مع ورثتهم) كـ DTO — للطلبات التي تلامس الكفلاء.</summary>
    public static List<GuarantorDto> MirrorGuarantors(Document doc) =>
        doc.Guarantors.OrderBy(g => g.GuarantorNumber)
            .Select(g => new GuarantorDto(
                g.Id, g.GuarantorNumber, g.GuarantorName, g.GuarantorFather, g.GuarantorFamily,
                g.GuarantorMother, g.GuarantorBirth, g.GuarantorRegister, g.GuarantorNationalId,
                g.GuarantorAddress, g.AddressType,
                g.RepresentativeName, g.RepresentativeFather, g.RepresentativeFamily,
                g.RepresentativeCapacity, g.RepresentativeAddressType, g.RepresentativeAddress,
                doc.Heirs.Where(h => h.GuarantorNumber == g.GuarantorNumber)
                    .Select(h => new HeirDto(h.Id, h.HeirName, h.HeirFather, h.HeirFamily,
                        h.HeirCapacity, h.AddressType, h.HeirAddress,
                        h.RepresentativeName, h.RepresentativeFather, h.RepresentativeFamily,
                        h.RepresentativeCapacity, h.RepresentativeAddressType, h.RepresentativeAddress)).ToList(),
                g.GuarantorNature, g.GuarantorRegistrationNumber, g.GuarantorRepresentedBy))
            .ToList();

    /// <summary>ورثة المقترض (بلا رقم كفيل) كـ DTO.</summary>
    public static List<HeirDto> MirrorBorrowerHeirs(Document doc) =>
        doc.Heirs.Where(h => h.GuarantorNumber is null)
            .Select(h => new HeirDto(h.Id, h.HeirName, h.HeirFather, h.HeirFamily,
                h.HeirCapacity, h.AddressType, h.HeirAddress,
                h.RepresentativeName, h.RepresentativeFather, h.RepresentativeFamily,
                h.RepresentativeCapacity, h.RepresentativeAddressType, h.RepresentativeAddress)).ToList();

    /// <summary>صفّ أصول المستند كـ DTO بهوياتها وتواريخها — لطلبات تعديل المنيب ذات
    /// الإنابات (قفل «الحجز بعد التسطير» يمنع إسقاط صف مرجعي أو تفريغ حجزه).</summary>
    public static List<AssetDto> MirrorAssets(Document doc) =>
        doc.Assets
            .Select(a => new AssetDto(
                a.Id, a.AssetKind,
                a.Owners.OrderBy(o => o.Order).Select(o => o.Name).ToList(),
                a.ShareType,
                a.Property, a.PropertyNumber, a.PropertyDistrict, a.LandRegistry,
                a.VehicleType, a.VehicleClass, a.PlateNumber, a.VehicleGovernorate,
                a.RegisterNumber, a.RegistrationDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), a.ShopGovernorate, a.ShopDescription, a.ShopLocation,
                a.PublicEntity,
                a.LicenseNumber, a.LicenseDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), a.LicenseIssuer,
                a.Notes,
                a.SeizureDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

    /// <summary>طلب تعديل يعيد بناء كل حقول المستند بما فيها الأطراف (الكفلاء وورثتهم وورثة المقترض).</summary>
    public static DocumentUpsertRequest MirrorRequestWithParties(Document doc)
    {
        var request = MirrorRequest(doc);
        request.Guarantors = MirrorGuarantors(doc);
        request.BorrowerHeirs = MirrorBorrowerHeirs(doc);
        return request;
    }
}
