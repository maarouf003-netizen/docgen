namespace DocGenerator.Domain.Entities;

/// <summary>
/// الشخص الطبيعي المنفذ عليه في وضع «منفذ عليه».
/// يحمل اسمه الثلاثي، ونوع العنوان (عنوان/وكيل) مع قيمته (العنوان أو الوكيل القانوني)،
/// وطريقة تمثيله (أصالةً أو إضافة لتركة مورث متوفى) مع حقول الاسم الثلاثي لمورثه عند الاختيار.
/// </summary>
public class ExecutedNaturalPerson
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? Name { get; set; }
    public string? Father { get; set; }
    public string? Family { get; set; }

    /// <summary>نوع العنوان/التمثيل: عنوان / وكيل.</summary>
    public string? AddressType { get; set; } = "عنوان";

    /// <summary>العنوان أو الوكيل القانوني — حسب AddressType.</summary>
    public string? AddressOrRepresentative { get; set; }

    /// <summary>طريقة التمثيل: أصالة / إضافة لتركة (Default أصالة).</summary>
    public string? RepresentationType { get; set; } = "أصالة";

    /// <summary>الاسم الثلاثي لمورث المطلوب التنفيذ عليه — يُعبأ عند اختيار «إضافة لتركة» أو «أصالة وإضافة».</summary>
    public string? DeceasedName { get; set; }
    public string? DeceasedFather { get; set; }
    public string? DeceasedFamily { get; set; }

    /// <summary>
    /// الممثل الشرعي للشخص الطبيعي (ولي/وصي/قيم) إن وُجد: اسمه الثلاثي وصفته وعنوانه.
    /// عند وجوده يصبح عنوان الممثل هو المعتبر.
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
    public ICollection<ExecutedHeir> Heirs { get; set; } = new List<ExecutedHeir>();
}
