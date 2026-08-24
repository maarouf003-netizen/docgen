namespace DocGenerator.Domain.Entities;

/// <summary>
/// رسالة ضمن كتاب المطالعة: الأصل (letter) أو اللاحق (addendum) من المحامي،
/// أو الرد (reply) من رئيس القسم. كل رسالة برقم وتاريخ مولّدين تلقائيًا عند الحفظ.
/// </summary>
public class ReviewLetterMessage
{
    /// <summary>الكتاب الأصلي المسطَّر من المحامي.</summary>
    public const string KindLetter = "letter";
    /// <summary>لاحق يضيفه محامي الكتاب بعد الإرسال.</summary>
    public const string KindAddendum = "addendum";
    /// <summary>رد رئيس القسم على الكتاب أو أحد اللاحقات.</summary>
    public const string KindReply = "reply";

    public int Id { get; set; }

    public int ReviewLetterId { get; set; }

    /// <summary>نوع الرسالة: letter / addendum / reply.</summary>
    public string Kind { get; set; } = KindLetter;

    /// <summary>نص الرسالة بصيغة HTML معقّم من المحرر الغني.</summary>
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>نص عادي مستخلص من HTML لأغراض البحث والمعاينة المختصرة.</summary>
    public string BodyPlainText { get; set; } = string.Empty;

    /// <summary>رقم الكتاب/اللاحق/الرد المولد تلقائيًا بصيغة {رمز الفرع}-{السنة}-{عشوائي}.</summary>
    public string MessageNumber { get; set; } = string.Empty;

    /// <summary>تاريخ إنشاء الرسالة (UTC).</summary>
    public DateTime MessageDate { get; set; } = DateTime.UtcNow;

    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    /// <summary>دور الكاتب: lawyer أو head.</summary>
    public string AuthorRole { get; set; } = "lawyer";

    /// <summary>
    /// هل اطّلع محامي الكتاب على هذا الرد؟ تُرفع عند فتحه للكتاب بعد الرد،
    /// وتغذي شارة «رد جديد» وعدّاد بند المطالعات.
    /// </summary>
    public bool IsSeenByLawyer { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ReviewLetter ReviewLetter { get; set; } = null!;
}
