namespace DocGenerator.Domain.Entities;

/// <summary>
/// وريث لمورثٍ متوفى في وضع «منفذ عليه». يُخزَّن الوريث مرتبطًا بالملف وبالمورث نفسه
/// (طالب التنفيذ المتوفى عبر ExecutionApplicantId، أو الشخص الطبيعي المنفذ عليه المتوفى
/// عبر ExecutedNaturalPersonId) بشكل احترافي يسمح بالبحث والتوسعة مستقبلًا:
/// تُنشأ فهارس على الملف وعلى كل مرجع مورث، ويفرض مستوى الخدمة أن يُملأ مرجع واحد
/// فقط (لا يمكن أن ينتمي الوريث لمورثين معًا).
/// </summary>
public class ExecutedHeir
{
    public int Id { get; set; }
    public int DocumentId { get; set; }

    /// <summary>مورث الوريث: طالب التنفيذ المتوفى (إن كان الوريث لطالب تنفيذ إضافة لتركة).</summary>
    public int? ExecutionApplicantId { get; set; }

    /// <summary>مورث الوريث: الشخص الطبيعي المنفذ عليه المتوفى (إن كان الوريث لمنفذ عليه إضافة لتركة).</summary>
    public int? ExecutedNaturalPersonId { get; set; }

    public string? HeirName { get; set; }
    public string? HeirFather { get; set; }
    public string? HeirFamily { get; set; }
    public string? AddressType { get; set; } = "عنوان";
    public string? HeirAddress { get; set; }

    public Document Document { get; set; } = null!;
    public ExecutionApplicant? ExecutionApplicant { get; set; }
    public ExecutedNaturalPerson? ExecutedNaturalPerson { get; set; }
}
