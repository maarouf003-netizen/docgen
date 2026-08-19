using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// إنابة تنفيذية: تسطير الدائرة المنيبة كتاب إنابة لدائرة أخرى للتنفيذ على عقار أو متجر
/// يتبع مكانيًا لاختصاص تلك الدائرة. يُنشأ الملف المناب عن الملف المنيب (نفس السند التنفيذي)
/// للتنفيذ على الأموال موضوع الإنابة فقط، وينتهي ببيعها بالمزاد العلني وإعادة الملف إلى
/// الدائرة المنيبة («منفذ إنابة»).
/// </summary>
public class DocumentDelegation
{
    public int Id { get; set; }

    /// <summary>الملف الأصلي (المنيب) الذي صدرت عنه الإنابة.</summary>
    public int SourceDocumentId { get; set; }

    /// <summary>
    /// الملف المناب المُنشأ عن هذه الإنابة (يُملأ عند اختيار المحامي المختص).
    /// الوجهة فريدة: كل إنابة تُنشئ ملفًا منابًا واحدًا، ويُعرَّف الملف المناب
    /// بمؤشر المفتاح الأجنبي الوحيد على Document.SourceDelegationId (بدون عمود مكرر هنا).
    /// </summary>
    public Document? TargetDocument { get; set; }

    /// <summary>الدائرة المنابة (دائرة التنفيذ في المحكمة المستهدفة) — حقل نص حر.</summary>
    public string? DelegatedCourt { get; set; }

    /// <summary>هل الإنابة خارجية (لمحافظة أخرى)؟ عندها يُذكر الفرع المناب والفرع المنيب.</summary>
    public bool IsExternal { get; set; }

    /// <summary>الفرع المناب المستهدف في المحافظة الأخرى (للكيان «الفرع»/Branch) عند الإنابة الخارجية.</summary>
    public int? ExternalBranchId { get; set; }

    /// <summary>تاريخ الإنابة (نص حر يُفسَّر ويُخزَّن زمنيًا كباقي تواريخ الملفات).</summary>
    public DateTime? DelegationDate { get; set; }

    /// <summary>منطوق الإنابة (بيان ما أُنيف به من أموال وإجراءات).</summary>
    public string? DelegationText { get; set; }

    /// <summary>رقم كتاب إيداع رئيس القسم كتاب الإنابة.</summary>
    public string? DepositBookNumber { get; set; }

    /// <summary>تاريخ كتاب إيداع رئيس القسم كتاب الإنابة (نص حر).</summary>
    public DateTime? DepositBookDate { get; set; }

    /// <summary>رقم كتاب إرسال الإنابة (للكيان «الإنابة الخارجية» إلى محافظة أخرى).</summary>
    public string? SendBookNumber { get; set; }

    /// <summary>تاريخ كتاب إرسال الإنابة (نص حر).</summary>
    public DateTime? SendBookDate { get; set; }

    /// <summary>المحامي المختص الذي يتابع الإنابة في الدائرة/الفرع المناب (يختاره رئيس القسم).</summary>
    public int? AssignedLawyerId { get; set; }

    /// <summary>تاريخ إعادة الملف المناب إلى الدائرة المنيبة بعد إتمام الإنابة (نص حر).</summary>
    public DateTime? ReturnDate { get; set; }

    /// <summary>
    /// حالة الإنابة (DelegationStatusCatalog): بانتظار رئيس القسم → محالة → مسجلة أصولًا → منفذ إنابة.
    /// </summary>
    public string Status { get; set; } = DelegationStatusCatalog.PendingHead;

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Document SourceDocument { get; set; } = null!;
    public Branch? ExternalBranch { get; set; }
    public User? AssignedLawyer { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<DelegationAsset> Assets { get; set; } = new List<DelegationAsset>();
}
