namespace DocGenerator.Domain.Entities;

/// <summary>
/// رقم أساس ملف التنفيذ لسنة ودائرة إدخال معينة — السجل الوحيد لتاريخ الأرقام.
/// لا يُحذف صف فيزيائيًا: الحذف/التراجع/استبدال القيد يضع <see cref="VoidedAt"/>
/// ويُبقي الصف معروضًا في «سجل أرقام الأساس».
/// التفرّد: (ExecutionCircuitId, TypeKey, Year, NumberKey) لغير الملغى فقط.
/// </summary>
public class DocumentBaseNumber
{
    public int Id { get; set; }
    public int DocumentId { get; set; }

    /// <summary>دائرة الإدخال — نطاق التفرد والهوية، وإلزامية لكل صف.</summary>
    public int? ExecutionCircuitId { get; set; }
    /// <summary>لقطة اسم الدائرة وقت الإدخال (تبقى بعد إعادة التسمية).</summary>
    public string? CircuitName { get; set; }

    public int Year { get; set; }
    /// <summary>لقطة نوع الملف وقت الإدخال (للعرض).</summary>
    public string? FileType { get; set; }
    /// <summary>النوع مطبَّعًا عبر ArabicNameNormalizer — مفتاح التفرد.</summary>
    public string TypeKey { get; set; } = string.Empty;
    /// <summary>الرقم للعرض.</summary>
    public string BaseNumber { get; set; } = string.Empty;
    /// <summary>الرقم مطبَّعًا (أرقام لاتينية بلا مسافات) — مفتاح التفرد والبحث.</summary>
    public string NumberKey { get; set; } = string.Empty;

    /// <summary>registration / rotation / renewal — للوسم والتراجع فقط.</summary>
    public string? Source { get; set; }

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedById { get; set; }

    /// <summary>null = فعّال؛ غير null = ملغى (يبقى معروضًا ولا يدخل الفهرس الفريد).</summary>
    public DateTime? VoidedAt { get; set; }
    public int? VoidedById { get; set; }
    /// <summary>document-deleted / undo-rotation / registration-superseded / registration-cleared.</summary>
    public string? VoidReason { get; set; }

    public Document Document { get; set; } = null!;
    public ExecutionCircuit? ExecutionCircuit { get; set; }
    public User CreatedBy { get; set; } = null!;
    public User? UpdatedBy { get; set; }
    public User? VoidedBy { get; set; }
}
