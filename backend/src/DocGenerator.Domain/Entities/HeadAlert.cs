using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// تنبيه يصدره رئيس القسم لفرعه، ويصل للمحامين كجهة استلام واحدة أو أكثر:
/// مرتبط بملف، أو رسالة خاصة لمحامٍ، أو تعميم لكل محامي الفرع.
/// الاستلام الفردي لكل محامٍ مسجّل في <see cref="HeadAlertRecipient"/>.
///
/// في إطار «التوسيع» (<c>M1</c>) تكون الحقول الجديدة nullable للتوافق الخلفي مع
/// الكود القديم؛ ويفرضها <c>M3</c> غير قابلة للفراغ. الوظائف الجديدة موضّحة
/// في سطورها.
/// </summary>
public class HeadAlert
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int CreatedById { get; set; }
    public HeadAlertTargetType TargetType { get; set; }

    /// <summary>
    /// النوع الدلالي للتنبيه من <see cref="HeadAlertKindCatalog"/> — بلا قيمة افتراضية صامتة.
    /// في <c>M1/M2</c> يبقى nullable للتوافق الخلفي؛ ويفرضه <c>M3</c> غير قابل للفراغ.
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// مفتاح منع التكرار/الدمج — nullable للتنبيهات اليدوية فقط، وإلزامي في كل تنبيه
    /// نظامي. يُبنى مركزيًا في <c>HeadAlertDedupe.Build</c> ولا يمرّره المستدعي نصًّا.
    /// </summary>
    public string? DedupeKey { get; set; }

    /// <summary>ربط دلالي بدائرة التنفيذ بدل مطابقة النص.</summary>
    public int? ExecutionCircuitId { get; set; }

    public int? DocumentId { get; set; }
    public int? TargetLawyerId { get; set; }
    /// <summary>رابط تنبيهات دورة حياة الإنابة (بانتظار الاعتماد/الإتمام) بالإنابة نفسها لتصفيتها تلقائيًا.</summary>
    public int? DelegationId { get; set; }
    /// <summary>رابط تنبيهات دورة حياة الاستئناف (بانتظار اختيار المحامي/الحسم/الشطب) بالاستئناف نفسه لتصفيتها تلقائيًا.</summary>
    public int? AppealId { get; set; }
    /// <summary>رابط تنبيهات الرد على كتاب المطالعة — للانتقال المباشر من التنبيه إلى الكتاب.</summary>
    public int? ReviewLetterId { get; set; }
    /// <summary>
    /// رابط التنبيه بالجهة العامة المتعلقة به (قيد/هوية). تُستعمل لدمج تنبيهات تعديل
    /// الجهة الواحدة قبل الاعتماد في تنبيه واحد بآخر تعديل: عند تكرار اقتراح التعديل
    /// على القيد نفسه قبل المراجعة يُحدَّث التنبيه القائم بدل إنشاء آخر.
    /// </summary>
    public int? PublicEntityId { get; set; }
    /// <summary>نص التنبيه — بلا سقف عمود (يُوسَّع إلى <c>text</c>/<c>TEXT</c> بلا قصّ صامت).</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>زمن الإنشاء الفعلي — ثابت لا يُعدَّل عند تغيّر المحتوى.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// زمن آخر تغيّر في المحتوى — للترتيب ولإعادة ضبط القراءة. يُحدَّث فقط عند تغيّر النص.
    /// في <c>M1/M2</c> يبقى nullable؛ ويفرضه <c>M3</c> غير قابل للفراغ.
    /// </summary>
    public DateTime? LastContentChangedAt { get; set; }

    /// <summary>أرشفة منطقية بدل الحذف النهائي — يستبعد الصف من القوائم والعدادات الافتراضية.</summary>
    public bool IsArchived { get; set; }

    /// <summary>زمن الأرشفة — يُضبط مع <see cref="IsArchived"/>.</summary>
    public DateTime? ArchivedAt { get; set; }

    public Branch? Branch { get; set; }
    public User? CreatedBy { get; set; }
    public Document? Document { get; set; }
    public User? TargetLawyer { get; set; }
    public DocumentAppeal? Appeal { get; set; }
    /// <summary>كتاب المطالعة المرتبط بالتنبيه (لتنبيهات الرد على المحامي).</summary>
    public ReviewLetter? ReviewLetter { get; set; }
    /// <summary>الجهة العامة المرتبطة بالتنبيه (لتنبيهات اقتراح التعديل والمراجعة).</summary>
    public PublicEntity? PublicEntity { get; set; }
    public ICollection<HeadAlertRecipient> Recipients { get; set; } = new List<HeadAlertRecipient>();
}
