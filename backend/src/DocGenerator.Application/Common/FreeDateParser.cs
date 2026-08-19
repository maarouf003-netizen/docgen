namespace DocGenerator.Application.Common;

/// <summary>
/// محلل «التاريخ الحر» المشترك (قاعدة Date Fields Rule): يحوّل نص التاريخ المرسل من النموذج
/// إلى DateTime? عبر ActionDateParser، والفراغ يعني null، والقيمة غير الصالحة (غير الفارغة) تُرفض
/// برسالة تحمل اسم الحقل. يستخدمه كل من يحلل تواريخ الملفات حتى لا يتكرر منطق التحقق.
/// </summary>
public static class FreeDateParser
{
    /// <summary>
    /// يحلل نص التاريخ الحر. يعيد null للفارغ/الأبيض، ويرمي ArgumentException لغير الصالح
    /// برسالة موحّدة: «{fieldName} غير صالح — استخدم مثال: 1/8/2026».
    /// </summary>
    public static DateTime? Parse(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parsed = ActionDateParser.TryParse(value);
        if (parsed is not null)
            return parsed;

        throw new ArgumentException($"{fieldName} غير صالح — استخدم مثال: 1/8/2026");
    }

    /// <summary>تنسيق استجابة التاريخ الحر لعرضه نصًا (yyyy-MM-dd) أو سلسلة فارغة عند الغياب.</summary>
    public static string? ToResponse(DateTime? value)
        => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
