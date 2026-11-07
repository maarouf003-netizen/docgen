namespace DocGenerator.Domain.Entities;

/// <summary>
/// ملحق السند التنفيذي (للعقد المصرفي): صف مستقل لكل ملحق بترتيبه.
/// الحد الأقصى 5 (AnnexLimitsCatalog)، والتاريخ نص حر.
/// </summary>
public class DocumentAnnex
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    /// <summary>ترتيب الملحق داخل الملف (يبدأ من 1).</summary>
    public int Sequence { get; set; }
    public string? AnnexType { get; set; }
    public string? AnnexNumber { get; set; }
    public string? AnnexDate { get; set; }
    public Document? Document { get; set; }
}
