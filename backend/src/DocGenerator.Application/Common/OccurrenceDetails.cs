namespace DocGenerator.Application.Common;

/// <summary>
/// فكّ حقل «تفاصيل الوقعة» المخزّن نصًا إلى شكليه المعروضين: قاموس JSON منضّم
/// (وقوعات تغيير الحالة اليدوية) أو سرد نصي حر (الوقوعات الآلية كنوع «تغيير جهة»).
/// العقد: أحدهما فقط غير null — إما Details أو DetailsText — فلا يُلوَّث القاموس بالنص الخام.
/// </summary>
public static class OccurrenceDetails
{
    /// <summary>
    /// يفكّ المخزون إلى (قاموس، سرد): JSON صالح → قاموس؛ نص حر غير JSON → سرد خام؛
    /// فارغ/أبيض → (null، null). لا يرمي أبدًا — العطب يُعامل كنص.
    /// </summary>
    public static (IReadOnlyDictionary<string, string>? Details, string? DetailsText) Split(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (null, null);
        try
        {
            return (System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(raw), null);
        }
        catch (System.Text.Json.JsonException)
        {
            return (null, raw);
        }
    }
}
