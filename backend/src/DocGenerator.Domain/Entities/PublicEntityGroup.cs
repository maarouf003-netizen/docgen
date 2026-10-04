namespace DocGenerator.Domain.Entities;

/// <summary>
/// الهوية الأم للجهة العامة في السجل المرجعي المركزي (11 نوعًا: مؤسسة عامة/شركة عامة/
/// مديرية/إدارة عامة/إدارة فرعية/هيئة عامة/أمانة عامة/محافظة/مجلس مدينة/مجلس بلدة/وزارة)
/// بالاسم المعتمد CanonicalName. تحتها قيود المستويين: المحافظة + الفرع.
/// </summary>
public class PublicEntityGroup
{
    public int Id { get; set; }

    /// <summary>الاسم المعتمد للجهة — فريد في السجل (max 200).</summary>
    public string CanonicalName { get; set; } = string.Empty;

    /// <summary>
    /// الاسم المعياري (`PB-001`): تطبيع `CanonicalName` للوحدانية الحقيقية —
    /// تضبطه الخدمة عند كل كتابة؛ فريد قاعديًا (الـ`NULL` مستثناة تقنيًا لكن
    /// الخدمة تضبطه دائمًا فلا `NULL` عمليًا).
    /// </summary>
    public string? CanonicalNameNorm { get; set; }

    /// <summary>نوع الجهة (كتالوج نصي: foundation/company/directorate/administration/sub-administration/authority/general-secretariat/governorate-body/city-council/town-council/ministry).</summary>
    public string EntityType { get; set; } = Enums.PublicEntityTypeCatalog.Ministry;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PublicEntity> Entries { get; set; } = new List<PublicEntity>();
}
