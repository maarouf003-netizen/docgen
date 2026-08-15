using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// طالب التنفيذ في وضع «منفذ عليه» — الجهة التي تقدّمت بالتنفيذ ضد المنفذ عليه.
/// يحمل هوية صاحبه (الاسم الثلاثي أو الاسم الاعتباري)، ووكيله القانوني إن وُجد،
/// وطريقة تمثيله (أصالةً أو إضافة لتركة مورث متوفى) مع حقول الاسم الثلاثي للمورث عند الاختيار.
/// </summary>
public class ExecutionApplicant
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? Name { get; set; }
    public string? Father { get; set; }
    public string? Family { get; set; }
    public string? LegalRepresentative { get; set; }

    /// <summary>
    /// طبيعة طالب التنفيذ/العرض: شخص طبيعي (natural) أو شخص اعتباري (legal).
    /// عند الاعتباري يحمل Name اسم الشخص الاعتباري، وتُصفَّر حقول الهوية الطبيعية.
    /// </summary>
    public string ApplicantNature { get; set; } = PartyNatureCatalog.Natural;
    /// <summary>رقم تسجيل الشخص الاعتباري عند الطبيعة الاعتبارية (اختياري).</summary>
    public string? ApplicantRegistrationNumber { get; set; }
    /// <summary>من يمثل الشخص الاعتباري عند الطبيعة الاعتبارية (اختياري).</summary>
    public string? ApplicantRepresentedBy { get; set; }
    /// <summary>نوع عنوان الشخص الاعتباري: موطن مختار / عنوان / وكيل قانوني.</summary>
    public string? ApplicantAddressType { get; set; }
    /// <summary>عنوان الشخص الاعتباري أو وكيله القانوني حسب نوع العنوان.</summary>
    public string? ApplicantAddress { get; set; }

    /// <summary>طريقة التمثيل: أصالة / إضافة لتركة (Default أصالة).</summary>
    public string? RepresentationType { get; set; } = "أصالة";

    /// <summary>الاسم الثلاثي للمورث المتوفى — يُعبأ عند اختيار «إضافة لتركة» أو «أصالة وإضافة».</summary>
    public string? DeceasedName { get; set; }
    public string? DeceasedFather { get; set; }
    public string? DeceasedFamily { get; set; }

    /// <summary>
    /// الممثل الشرعي لطالب التنفيذ (ولي/وصي/قيم) إن وُجد: اسمه الثلاثي وصفته ووكيله القانوني.
    /// عند وجوده يصبح وكيل الممثل هو المعتبر ويُخفى حقل «الوكيل القانوني» لطالب التنفيذ نفسه.
    /// </summary>
    public string? RepresentativeName { get; set; }
    public string? RepresentativeFather { get; set; }
    public string? RepresentativeFamily { get; set; }
    /// <summary>صفة الممثل الشرعي: ولي / وصي / قيم.</summary>
    public string? RepresentativeCapacity { get; set; }
    /// <summary>الوكيل القانوني للممثل الشرعي.</summary>
    public string? RepresentativeLegalRepresentative { get; set; }

    public Document Document { get; set; } = null!;
    public ICollection<ExecutedHeir> Heirs { get; set; } = new List<ExecutedHeir>();
}
