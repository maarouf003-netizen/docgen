namespace DocGenerator.Domain.Entities;

/// <summary>
/// دائرة التنفيذ: دائرة المحكمة التي يُسجَّل بها الملف التنفيذي.
/// مرتبطة بفرع الإدارة (BranchId إلزامي) إداريًا، وقائمة محافظتها تُشتق بمطابقة
/// Governorate (بلا افتراض فرعٍ واحدٍ للمحافظة). التعطيل إخفاء من قوائم «اختيار»
/// فقط (IsActive=false) بلا حذف ولا نقل؛ الحذف لا يُتاح إلا بصفر ملفات.
/// </summary>
public class ExecutionCircuit
{
    public int Id { get; set; }

    /// <summary>فرع الإدارة المالك للدائرة (إلزامي، لا يتغير أبدًا).</summary>
    public int BranchId { get; set; }

    /// <summary>
    /// الشعبة المالكة للدائرة؛ `null` = ملك القسم مباشرة. يُضبط من المنشئ عند
    /// الإنشاء (دائرة رئيس الشعبة تلحق بشعبته)، والنقل لاحقًا بإجراء «نقل دائرة».
    /// </summary>
    public int? SectionId { get; set; }

    /// <summary>اسم الدائرة (200).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الاسم المعياري (تطبيع ArabicNameNormalizer نفسه): فريد × BranchId.
    /// </summary>
    public string NameNorm { get; set; } = string.Empty;

    /// <summary>التفعيل: false = معطلة (إخفاء من «اختيار» فقط).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>رئيس القسم المنشئ.</summary>
    public int CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>عدّاد التزامن المتفائل (مثل Document.Version).</summary>
    public long Version { get; set; }

    public Branch? Branch { get; set; }
    public Section? Section { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
