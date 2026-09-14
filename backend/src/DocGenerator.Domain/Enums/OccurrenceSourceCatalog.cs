namespace DocGenerator.Domain.Enums;

/// <summary>
/// مصدر وقعة الملف: «نظامي» (سجّلها النظام آليًا خلال إجراءٍ واقعي مثل الشطب أو التجديد أو
/// تغيير جهة أو الإنابة) أم «يدوي» (أدخلها المحرر من واجهة سجل الوقوعات). النظامي لا يُعدَّل
/// ولا يُحذف من الواجهة — التقسية. يُخزَّن بالإنكليزية في القاعدة.
/// </summary>
public static class OccurrenceSourceCatalog
{
    /// <summary>وقعة سجّلها النظام تلقائيًا أثناء إجراء حقيقي (شطب/تجديد/تغيير حالة/تغيير جهة/إنابة).</summary>
    public const string System = "system";

    /// <summary>وقعة أدخلها المحرر يدويًا من سجل الوقوعات (قابلة للتعديل والحذف).</summary>
    public const string Manual = "manual";

    public static bool IsSystem(string? source) => source == System;
}