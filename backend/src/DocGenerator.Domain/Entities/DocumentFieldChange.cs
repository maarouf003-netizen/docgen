namespace DocGenerator.Domain.Entities;

/// <summary>
/// تغيّر حقل واحد ضمن تعديل ملف تنفيذي: يرتبط بإدخال سجل التدقيق الذي أنتجه،
/// ويوثّق اسم الحقل بتسميته العربية المجمّدة وقت الكتابة وقيمته قبل وبعد.
/// أداة المراجعة المؤسسية لتتبع كل تعديل مهما صغُر على مستوى الحقل.
/// </summary>
public class DocumentFieldChange
{
    public int Id { get; set; }

    public int AuditLogId { get; set; }

    /// <summary>الملف المعني — منزال للتسريع (نفس قيمة إدخال التدقيق الأب).</summary>
    public int DocumentId { get; set; }

    /// <summary>مفتاح الحقل المستقر (اسم الخاصية) للبرمجة والتصفية.</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>التسمية العربية المعروضة، مجمّدة عند الكتابة لثبات الأرشيف.</summary>
    public string FieldLabel { get; set; } = string.Empty;

    /// <summary>القيمة قبل التعديل منسّقة للعرض؛ فارغة عند الإضافة الأولى للحقل.</summary>
    public string? OldValue { get; set; }

    /// <summary>القيمة بعد التعديل منسّقة للعرض؛ فارغة عند تصفير الحقل.</summary>
    public string? NewValue { get; set; }

    public AuditLog AuditLog { get; set; } = null!;
}
