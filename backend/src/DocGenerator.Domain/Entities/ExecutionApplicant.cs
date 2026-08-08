namespace DocGenerator.Domain.Entities;

/// <summary>
/// طالب التنفيذ في وضع «منفذ عليه» — الجهة التي تقدّمت بالتنفيذ ضد المنفذ عليه.
/// يحمل هوية صاحبه (الاسم الثلاثي)، ووكيله القانوني إن وُجد، وطريقة تمثيله
/// (أصالةً أو إضافة لتركة مورث متوفى) مع حقول الاسم الثلاثي للمورث عند الاختيار.
/// </summary>
public class ExecutionApplicant
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? Name { get; set; }
    public string? Father { get; set; }
    public string? Family { get; set; }
    public string? LegalRepresentative { get; set; }

    /// <summary>طريقة التمثيل: أصالة / إضافة لتركة (Default أصالة).</summary>
    public string? RepresentationType { get; set; } = "أصالة";

    /// <summary>الاسم الثلاثي للمورث المتوفى — يُعبأ عند اختيار «إضافة لتركة».</summary>
    public string? DeceasedName { get; set; }
    public string? DeceasedFather { get; set; }
    public string? DeceasedFamily { get; set; }

    public Document Document { get; set; } = null!;
    public ICollection<ExecutedHeir> Heirs { get; set; } = new List<ExecutedHeir>();
}
