namespace DocGenerator.Domain.Entities;

/// <summary>
/// سجل تعاقب المحامين على الملف: منشئ الملف وكل محامٍ حُمّل عليه الملف مع تاريخ الإحالة.
/// يُنشأ سجل عند إنشاء الملف (create) وعند كل نقل/إحالة (transfer) داخل المعاملة نفسها.
/// </summary>
public class DocumentAssignment
{
    public int Id { get; set; }
    public int DocumentId { get; set; }

    /// <summary>نوع السجل: create = منشئ الملف، transfer = إحالة إلى محامٍ.</summary>
    public string Kind { get; set; } = "create";

    /// <summary>اسم المحامي الحامل للملف (منشئ الملف أو المستلم عند الإحالة).</summary>
    public string LawyerName { get; set; } = string.Empty;

    /// <summary>من نفّذ الإحالة (لسجلات transfer فقط) — فارغ لسجل الإنشاء.</summary>
    public string? AssignedByName { get; set; }

    /// <summary>لحظة الإحالة أو الإنشاء (UTC).</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
}
