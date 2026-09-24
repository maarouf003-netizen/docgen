namespace DocGenerator.Domain.Entities;

/// <summary>
/// رسالة ضمن مراسلة: الأصل (letter) من المنشئ، أو اللاحق (addendum) الذي يضيفه المنشئ نفسه،
/// أو الرد (reply) من الطرف المستلم. كل رسالة برقم وتاريخ مولّدين تلقائيًا عند الحفظ.
/// </summary>
public class CorrespondenceMessage
{
    /// <summary>المراسلة الأصلية المسطَّرة من المنشئ.</summary>
    public const string KindLetter = "letter";

    /// <summary>لاحق يضيفه منشئ المراسلة بعد الإرسال.</summary>
    public const string KindAddendum = "addendum";

    /// <summary>رد الطرف المستلم على المراسلة أو أحد اللاحقات.</summary>
    public const string KindReply = "reply";

    public int Id { get; set; }

    public int CorrespondenceId { get; set; }

    /// <summary>نوع الرسالة: letter / addendum / reply.</summary>
    public string Kind { get; set; } = KindLetter;

    /// <summary>نص الرسالة بصيغة HTML معقّم من المحرر الغني.</summary>
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>نص عادي مستخلص من HTML لأغراض البحث والمعاينة المختصرة.</summary>
    public string BodyPlainText { get; set; } = string.Empty;

    /// <summary>رقم المراسلة/اللاحق/الرد المولد تلقائيًا بصيغة {الرمز}-{السنة}-{عشوائي}.</summary>
    public string MessageNumber { get; set; } = string.Empty;

    /// <summary>تاريخ إنشاء الرسالة (UTC).</summary>
    public DateTime MessageDate { get; set; } = DateTime.UtcNow;

    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>دور الكاتب: lawyer أو head أو entitymanager.</summary>
    public string AuthorRole { get; set; } = "lawyer";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Correspondence Correspondence { get; set; } = null!;
}
