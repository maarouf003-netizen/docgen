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
    public string? AddressType { get; set; } = "عنوان";
    public string? HeirAddress { get; set; }

    public Document Document { get; set; } = null!;
}
