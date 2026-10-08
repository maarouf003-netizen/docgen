namespace DocGenerator.Domain.Enums;

/// <summary>
/// حالة إحالة الاستئناف (شعبة → قسم): رموز إنكليزية داخلية لا تُعرض خامًا.
/// </summary>
public static class AppealForwardCatalog
{
    /// <summary>ملك نطاقه الأصلي (دائرة الملف) — الحالة الافتراضية.</summary>
    public const string Owned = "Owned";

    /// <summary>محال لرئيس قسم نفس الفرع — استثناء قرائي حتى الحسم.</summary>
    public const string ForwardedToHead = "ForwardedToHead";

    public static readonly IReadOnlySet<string> ValidStates = new HashSet<string>
    {
        Owned, ForwardedToHead,
    };
}

/// <summary>
/// أحداث سجل التعاقب (`HeadSuccession`): رموز إنكليزية داخلية.
/// </summary>
public static class HeadSuccessionEventCatalog
{
    public const string Appointed = "appointed";
    public const string Deactivated = "deactivated";
    public const string Succeeded = "succeeded";
    public const string CircuitTransferred = "circuit-transferred";
    public const string Renamed = "renamed";

    public static readonly IReadOnlySet<string> ValidEvents = new HashSet<string>
    {
        Appointed, Deactivated, Succeeded, CircuitTransferred, Renamed,
    };
}
