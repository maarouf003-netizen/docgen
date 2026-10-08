using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// سجل تعاقب الرئاسة (قراءة فقط: كتابة بلا تعديل/حذف): يوثّق كل حدث تعيين أو
/// تعطيل أو إحلال أو نقل دائرة أو تعديل اسم. يُكتب ضمن معاملة الحدث نفسه.
/// </summary>
public class HeadSuccession
{
    public int Id { get; set; }

    /// <summary>الفرع الذي وقع فيه الحدث.</summary>
    public int BranchId { get; set; }

    /// <summary>الشعبة المعنية؛ null لأحداث رئيس القسم.</summary>
    public int? SectionId { get; set; }

    /// <summary>الحساب موضوع الحدث (المعيَّن/المعطَّل/الخلف).</summary>
    public int UserId { get; set; }

    /// <summary>دور الحساب موضوع الحدث (`Head`/`SubHead`).</summary>
    public UserRole Role { get; set; } = UserRole.Head;

    /// <summary>نوع الحدث (`HeadSuccessionEventCatalog`).</summary>
    public string Event { get; set; } = string.Empty;

    /// <summary>لحظة الحدث (UTC).</summary>
    public DateTime At { get; set; } = DateTime.UtcNow;

    /// <summary>اسم الفاعل البشري (مدير/مشرف).</summary>
    public string? ActorName { get; set; }

    /// <summary>سبب الحدث (إلزامي معنًى عند التعطيل/النقل).</summary>
    public string? Reason { get; set; }

    public Branch? Branch { get; set; }
    public Section? Section { get; set; }
    public User? User { get; set; }
}
