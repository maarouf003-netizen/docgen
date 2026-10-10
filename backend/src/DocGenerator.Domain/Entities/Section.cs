namespace DocGenerator.Domain.Entities;

/// <summary>
/// شعبة داخل فرع إدارة: واحد لواحد مع رئيس شعبة مفعّل واحد. الدوائر
/// (`ExecutionCircuit.SectionId`) والمستخدمون (`User.SectionId`) يتبعونها؛
/// `null` في أي منهما يعني ملك القسم مباشرة.
/// </summary>
public class Section
{
    public int Id { get; set; }

    /// <summary>فرع الإدارة الأب (إلزامي، لا يتغير أبدًا — النقل داخل الفرع نفسه).</summary>
    public int BranchId { get; set; }

    /// <summary>اسم الشعبة (200).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الاسم المعياري (تطبيع ArabicNameNormalizer نفسه): فريد × BranchId.
    /// </summary>
    public string NameNorm { get; set; } = string.Empty;

    /// <summary>التفعيل: false = معطلة (إخفاء من «اختيار» فقط).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Branch? Branch { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<ExecutionCircuit> Circuits { get; set; } = new List<ExecutionCircuit>();
}
