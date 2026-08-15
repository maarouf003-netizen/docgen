using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

public class Guarantor
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int GuarantorNumber { get; set; }
    public string? GuarantorName { get; set; }
    public string? GuarantorFather { get; set; }
    public string? GuarantorFamily { get; set; }
    public string? GuarantorMother { get; set; }
    public string? GuarantorBirth { get; set; }
    public string? GuarantorRegister { get; set; }
    public string? GuarantorNationalId { get; set; }
    public string? GuarantorAddress { get; set; }
    public string? AddressType { get; set; } = "موطن مختار";

    /// <summary>
    /// طبيعة الكفيل: شخص طبيعي (natural) أو شخص اعتباري (legal).
    /// عند الاعتباري يحمل GuarantorName اسم الشخص الاعتباري، وتُصفَّر حقول الهوية الطبيعية.
    /// </summary>
    public string GuarantorNature { get; set; } = PartyNatureCatalog.Natural;
    /// <summary>رقم تسجيل الشخص الاعتباري عند الطبيعة الاعتبارية (اختياري).</summary>
    public string? GuarantorRegistrationNumber { get; set; }
    /// <summary>من يمثل الشخص الاعتباري عند الطبيعة الاعتبارية (اختياري).</summary>
    public string? GuarantorRepresentedBy { get; set; }

    /// <summary>
    /// الممثل الشرعي للكفيل (ولي/وصي/قيم) إن وُجد: اسمه الثلاثي وصفته وعنوانه.
    /// عند وجوده يصبح عنوان الممثل هو المعتبر ويُخفى عنوان الكفيل نفسه.
    /// </summary>
    public string? RepresentativeName { get; set; }
    public string? RepresentativeFather { get; set; }
    public string? RepresentativeFamily { get; set; }
    /// <summary>صفة الممثل الشرعي: ولي / وصي / قيم.</summary>
    public string? RepresentativeCapacity { get; set; }
    /// <summary>نوع عنوان الممثل الشرعي: موطن مختار / عنوان / وكيل قانوني.</summary>
    public string? RepresentativeAddressType { get; set; }
    /// <summary>عنوان الممثل الشرعي أو وكيله القانوني حسب نوع العنوان.</summary>
    public string? RepresentativeAddress { get; set; }

    public Document Document { get; set; } = null!;
}
