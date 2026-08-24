namespace DocGenerator.Domain.Entities;

/// <summary>
/// كتاب مطالعة يسطّره المحامي لرئيس قسمه (مرتبطًا بملف تنفيذي أو عامًا غير مرتبط).
/// يُولَّد رقمه تلقائيًا بصيغة {رمز الفرع}-{السنة}-{عشوائي} عند الحفظ، ويؤخذ تاريخه من لحظة الإرسال.
/// الكتاب المرسل وثيقة رسمية: لا يُعدَّل نصه ولا يُحذف، ويُستكمل بلاحقٍ أو ردود فقط.
/// </summary>
public class ReviewLetter
{
    public int Id { get; set; }

    public int BranchId { get; set; }

    /// <summary>المحامي الذي سطّر الكتاب.</summary>
    public int CreatedById { get; set; }

    /// <summary>الملف التنفيذي المرتبط؛ null يعني «كتاب مطالعة عام غير مرتبط بملف».</summary>
    public int? DocumentId { get; set; }

    /// <summary>رقم كتاب المطالعة المولد تلقائيًا (فريد).</summary>
    public string LetterNumber { get; set; } = string.Empty;

    /// <summary>تاريخ إرسال كتاب المطالعة (UTC) المعروض في السجلات.</summary>
    public DateTime LetterDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// حالة الكتاب: false = بانتظار رد رئيس القسم، true = تم الرد.
    /// أي لاحق جديد يعيده إلى بانتظار الرد.
    /// </summary>
    public bool IsAnswered { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Branch? Branch { get; set; }
    public User? CreatedBy { get; set; }
    public Document? Document { get; set; }

    public ICollection<ReviewLetterMessage> Messages { get; set; } = new List<ReviewLetterMessage>();
}
