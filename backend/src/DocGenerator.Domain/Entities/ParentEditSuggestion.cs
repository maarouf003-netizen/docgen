using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// اقتراح رئيس قسم لتعديل بيانات الجهة الأم (IsParentEntity) بلا كتابة مباشرة على القيد:
/// يُسلَّم للمدير/المشرف للقبول (فيُوجَّه لشاشة إعادة التسمية المركزية) أو الرفض مع سبب.
/// لا يُعدِّل هذا الكيان الجهة إطلاقًا — هو مجرد طلب تنفيه الإدارة.
/// </summary>
public class ParentEditSuggestion
{
    public int Id { get; set; }

    /// <summary>الهوية الأم المتأثرة (FK إلزامي).</summary>
    public int GroupId { get; set; }

    /// <summary>قيد الجهة الأم المتأثر (FK إلزامي — القيد ذو IsParentEntity=true).</summary>
    public int EntryId { get; set; }

    /// <summary>الاسم المعتمد المقترح (اختياري: يُترك فارغًا إذا كان العرض عن الاسم الحالي).</summary>
    public string? ProposedCanonicalName { get; set; }

    /// <summary>نوع الجهة المقترح من الكتالوج (اختياري).</summary>
    public string? ProposedEntityType { get; set; }

    /// <summary>صيغة المناداة المقترحة (اختياري).</summary>
    public string? ProposedCitationFormula { get; set; }

    /// <summary>سبب الاقتراح (إلزامي).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>الحالة: pending / approved / rejected / withdrawn.</summary>
    public string Status { get; set; } = ParentEditSuggestionStatusCatalog.Pending;

    /// <summary>رئيس القسم صاحب الاقتراح (FK إلزامي).</summary>
    public int CreatedById { get; set; }

    /// <summary>فرع الرئيس صاحب الاقتراح — يُستخدم للفهرس الفريد ومنع المكرر المعلّق.</summary>
    public int CreatedBranchId { get; set; }

    /// <summary>من راجع الاقتراح (مدير/مشرف) — null ما دام معلّقًا.</summary>
    public int? ReviewedById { get; set; }

    /// <summary>سبب الرفض (إلزامي عند الرفض) أو ملاحظة القبول.</summary>
    public string? ReviewReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>لحظة القبول/الرفض — null ما دام معلّقًا.</summary>
    public DateTime? ReviewedAtUtc { get; set; }

    public PublicEntityGroup Group { get; set; } = null!;

    public PublicEntity Entry { get; set; } = null!;

    public User CreatedBy { get; set; } = null!;

    public User? ReviewedBy { get; set; }
}