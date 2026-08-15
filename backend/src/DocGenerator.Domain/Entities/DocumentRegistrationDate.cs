namespace DocGenerator.Domain.Entities;

public class DocumentRegistrationDate
{
    public int DocumentId { get; set; }

    /// <summary>التاريخ النصي المعروض (صيغة قابلة للقراءة) كما أدخلها المستخدم.</summary>
    public string? Date { get; set; }

    /// <summary>التاريخ محلول كزمن حقيقي لفلترة الفترات في قاعدة البيانات
    /// (تُحفظ عند الكتابة وتُعبأ من النص عند الهجرة).</summary>
    public DateTime? DateParsed { get; set; }

    public Document Document { get; set; } = null!;
}
