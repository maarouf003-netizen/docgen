namespace DocGenerator.Domain.Entities;

/// <summary>
/// وقعة من وقوعات الملف في وضع «منفذ عليه»/«عرض وايداع»: شطب (struck-off) أو تجديد (renewal).
/// سجل زمني مستقل يسمح بتكرار الشطب والتجديد أكثر من مرة دون فقدان الوقعات السابقة.
/// حقول التجديد (الرقم/النوع/السنة/ورود الاخطار) تخص وقعة التجديد.
/// </summary>
public class DocumentOccurrence
{
    public int Id { get; set; }
    public int DocumentId { get; set; }

    /// <summary>نوع الوقعة: "struck-off" (شطب) أو "renewal" (تجديد).</summary>
    public string OccurrenceType { get; set; } = string.Empty;

    /// <summary>تاريخ الوقعة: تاريخ الشطب أو تاريخ التجديد (نص حر يُفسَّر ويُخزَّن زمنيًا).</summary>
    public DateTime? EventDate { get; set; }

    /// <summary>الرقم المعني بالوقعة: الرقم الذي حُمّل عليه الملف عند الشطب (القديم) أو الرقم الجديد للتجديد.</summary>
    public string? FileNumber { get; set; }

    /// <summary>نوع الملف الجديد عند التجديد (اختياري).</summary>
    public string? FileType { get; set; }

    /// <summary>سنة الوقعة: سنة الشطب أو سنة الإعادة للتجديد.</summary>
    public int? Year { get; set; }

    /// <summary>رقم ورود اخطار التجديد عند التجديد (اختياري).</summary>
    public string? ReceiptNumber { get; set; }

    /// <summary>تاريخ ورود اخطار التجديد عند التجديد (اختياري، نص حر).</summary>
    public DateTime? ReceiptDate { get; set; }

    /// <summary>
    /// حقول الإجراء الكاملة (JSON) لوقوعات تغيير الحالة: براءة الذمة/التريث/السير بالملف،
    /// المبالغ المحصلة بعملاتها، نوع التنفيذ الفرعي، العقارات المباعة، إلخ — تُحفظ لقطةً
    /// عند تسجيل الوقعة لتبقى ظاهرة بعد أي تراجع أو تعديل لاحق للحالة.
    /// </summary>
    public string? Details { get; set; }

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}