namespace DocGenerator.Domain.Entities;

/// <summary>
/// وريث لمنفذ عليه متوفى (المقترض أو أحد الكفلاء). يُحل الورثة محل المتوفى في
/// المخاصمة: يُذكر اسم المتوفى وهم «ورثة المتوفى»، ويُوجَّه كل إخطار إلى كل وريث على حدة.
/// ربط الوريث بالمنفذ عليه عبر GuarantorNumber (وليس FK لكيان الكفيل) لأن الكفلاء
/// يُعاد إنشاؤهم بمعرفات جديدة عند كل تعديل بينما الرقم ثابت، وnull يعني ورثة المقترض.
/// </summary>
public class Heir
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int? GuarantorNumber { get; set; }
    public string? HeirName { get; set; }
    public string? HeirFather { get; set; }
    public string? HeirFamily { get; set; }

    /// <summary>صفة الوريث: أصالة / إضافة لتركة / أصالة وإضافة (Default أصالة).</summary>
    public string? HeirCapacity { get; set; } = "أصالة";

    public string? AddressType { get; set; } = "عنوان";
    public string? HeirAddress { get; set; }

    /// <summary>
    /// الممثل الشرعي للوريث (ولي/وصي/قيم) إن وُجد: اسمه الثلاثي وصفته وعنوانه.
    /// عند وجوده يصبح عنوان الممثل هو المعتبر ويُصفَّر عنوان الوريث.
    /// لا يجتمع الممثل الشرعي مع ورثة الشخص نفسه.
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
