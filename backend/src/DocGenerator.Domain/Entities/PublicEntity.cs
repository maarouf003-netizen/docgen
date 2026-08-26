using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// قيد الجهة في السجل بمستوى المحافظة + الفرع (د2): تابعًا لهوية أم (Group).
/// Status = Final يظهر لبوات المندوبين، وPending لا يظهر إلا لمالكه المحامي
/// على ملفه حتى الاعتماد (د4). CitationFormula صيغة مناداة ممثلها القانوني (د8).
/// </summary>
public class PublicEntity
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    /// <summary>المحافظة (max 100) — من كتالوج المحافظات المعتمد في الواجهة.</summary>
    public string Governorate { get; set; } = string.Empty;

    /// <summary>اسم الفرع (max 200) — «الفرع الرئيسي» قيمة افتراضية مسموحة.</summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// تسمية التغطية (max 150): تصف النطاق الجغرافي للقيد إذا كان يخدم أكثر من محافظة.
    /// يُعرض في البطاقات والبحث بدل المحافظة (CoverageLabel ?? Governorate)؛
    /// الحوكمة والفلترة والتجميع والفهرس الفريد تبقى على Governorate حصرًا.
    /// يُرفض إذا طابق اسم محافظة واحدة من الكتالوج.
    /// </summary>
    public string? CoverageLabel { get; set; }

    /// <summary>صيغة المناداة (د8): add-to-job / add-to-position.</summary>
    public string CitationFormula { get; set; } = CitationFormulaCatalog.AddToJob;

    /// <summary>حالة القيد: final / pending (د4).</summary>
    public string Status { get; set; } = EntityStatusCatalog.Final;

    public int CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// نموذج الحوكمة الجديد: الجهة التي أدخلها محامٍ تُعتمد أصوليًا نهائية فورًا
    /// لكنها تبقى «بحاجة مراجعة» حتى يعتمدها/عدّلها رئيس قسم محافظتها أو الإدارة.
    /// </summary>
    public bool NeedsReview { get; set; }

    /// <summary>لحظة إقفال المراجعة (اعتمادًا أو تعديلًا) — null ما دامت قيد المراجعة.</summary>
    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>من أقفل المراجعة.</summary>
    public int? ReviewedById { get; set; }

    public User? ReviewedBy { get; set; }

    public bool IsActive { get; set; } = true;

    public PublicEntityGroup Group { get; set; } = null!;

    public User CreatedBy { get; set; } = null!;

    public ICollection<PublicEntityAlias> Aliases { get; set; } = new List<PublicEntityAlias>();
}
