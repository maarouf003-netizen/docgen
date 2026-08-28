namespace DocGenerator.Domain.Enums;

/// <summary>
/// كتالوج نوع الجهة العامة (الهوية الأم): وزارة/إدارة/هيئة/مؤسسة/شركة.
/// يُخزَّن نصيًا (قيم إنكليزية صغيرة) اتساقًا مع نمط PartyNatureCatalog.
/// </summary>
public static class PublicEntityTypeCatalog
{
    public const string Ministry = "ministry";
    public const string Administration = "administration";
    public const string Authority = "authority";
    public const string Foundation = "foundation";
    public const string Company = "company";

    /// <summary>كل القيم المسموحة بالترتيب المعتمد للعرض.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Ministry, Administration, Authority, Foundation, Company,
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim(), StringComparer.Ordinal);
}

/// <summary>
/// حالة قيد الجهة في السجل: Final (معتمدة وتظهر لبوات المندوبين) /
/// Pending (بانتظار اعتماد رئيس القسم — لا تظهر للمندوبين).
/// </summary>
public static class EntityStatusCatalog
{
    public const string Final = "final";
    public const string Pending = "pending";

    public static bool IsValid(string? value)
        => value is Final or Pending;

    /// <summary>
    /// هل القيد «مُعتمد نهائيًا» ويظهر لبوات المندوبين؟
    /// المستهلكون النهائيون (بوابة المندوب: النطاق/التصدير/الإحصاءات) يُضمّنون
    /// هذا الشرط حرفيًا في الاستعلام (قرار المواءمة السلوكية §6bis): أي قيد دخل
    /// به محامٍ و`NeedsReview=true` لا يظهر لأي مندوب قبل اعتماد رئيس القسم،
    /// رغم أنه مُخزَّن بـ `Status=Final`. يُستخدم للتوثيق فقط — لا يصلح داخل أشجار
    /// التعبير المُترجَمة إلى SQL، لذا يُكتب الشرط هنا صراحةً في كل مصرف نهائي.
    /// </summary>
    public static bool IsApproved(string status, bool needsReview)
        => status == Final && !needsReview;
}

/// <summary>
/// صيغة مناداة ممثل الجهة القانونية (د8): تُخزَّن على القيد وتُعرض عند توليد/
/// عرض ممثلها القانوني لاحقًا: «إضافة لوظيفته» أو «إضافة لمنصبه».
/// </summary>
public static class CitationFormulaCatalog
{
    public const string AddToJob = "add-to-job";
    public const string AddToPosition = "add-to-position";

    public static bool IsValid(string? value)
        => value is AddToJob or AddToPosition;
}
