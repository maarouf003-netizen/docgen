namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لأنواع الأموال (الأصول) المدعومة في الملف وتسمياتها العربية.
/// تُخزَّن القيم في قاعدة البيانات وتُعرَّض للواجهة كنصوص عربية (توافق مع البيانات القائمة)،
/// ويُحصر تعريف هذه النصوص في هذا الكتالوج بدل تكرارها في الخدمة والواجهة.
/// </summary>
public static class AssetKindCatalog
{
    /// <summary>عقار (غير منقول): رقم العقار والمنطقة العقارية والمصالح العقارية.</summary>
    public const string RealEstate = "عقار";

    /// <summary>مركبة (منقول): النوع والفئة ورقم اللوحة والمحافظة.</summary>
    public const string Vehicle = "مركبة";

    /// <summary>متجر مسجل (منقول بحكم القانون): رقم السجل وتاريخ التسجيل والمحافظة والوصف والموقع.</summary>
    public const string Shop = "متجر";

    /// <summary>كفالة رواتب (منقول): صاحب الراتب والجهة العامة والملاحظات — لا مقدار حصة ولا يظهر في «منفذ جبريا».</summary>
    public const string SalaryGuarantee = "كفالة رواتب";

    /// <summary>متجر غير مسجل (منقول): رقم الترخيص وتاريخه والجهة المصدرة والمالك والملاحظات.</summary>
    public const string UnregisteredShop = "متجر غير مسجل";

    public static readonly IReadOnlySet<string> ValidKinds = new HashSet<string>
    {
        RealEstate, Vehicle, Shop, SalaryGuarantee, UnregisteredShop,
    };

    /// <summary>الأنواع التي تحمل مقدار حصة («تمام …» / «حصة سهمية»): العقار والمركبة والمتجر.</summary>
    public static readonly IReadOnlySet<string> ShareableKinds = new HashSet<string>
    {
        RealEstate, Vehicle, Shop,
    };

    /// <summary>الأنواع التي يجوز بيعها بالمزاد العلني في «منفذ جبريا» (كفالة الرواتب مستثناة).</summary>
    public static readonly IReadOnlySet<string> AuctionableKinds = new HashSet<string>
    {
        RealEstate, Vehicle, Shop, UnregisteredShop,
    };

    public static bool IsValid(string? kind) => kind is not null && ValidKinds.Contains(kind);

    /// <summary>هل يحمل هذا النوع مقدار حصة (تمام/حصة سهمية)؟</summary>
    public static bool HasShare(string? kind) => kind is not null && ShareableKinds.Contains(kind);

    /// <summary>هل يجوز بيع هذا النوع بالمزاد العلني في «منفذ جبريا»؟</summary>
    public static bool IsAuctionable(string? kind) => kind is not null && AuctionableKinds.Contains(kind);

    /// <summary>قيمة «تمام» الخاصة بالنوع (تمام العقار / تمام المركبة / تمام المتجر).</summary>
    public static string FullShareLabel(string? kind) => kind switch
    {
        Vehicle => "تمام المركبة",
        Shop => "تمام المتجر",
        _ => "تمام العقار",
    };

    /// <summary>تسمية النوع للعرض العام (تُستخدم في «منفذ جبريا» وفي قوائم الاختيار).</summary>
    public static string ToLabel(string? kind) => kind switch
    {
        RealEstate => RealEstate,
        Vehicle => Vehicle,
        Shop => Shop,
        SalaryGuarantee => SalaryGuarantee,
        UnregisteredShop => UnregisteredShop,
        _ => RealEstate,
    };
}
