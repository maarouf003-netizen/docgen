using System.Globalization;

namespace DocGenerator.Application.Common;

/// <summary>
/// يحلّل تاريخ النص المفتوح (تواريخ القيد والإجراءات) إلى زمن حقيقي. الصيغ المعتمدة
/// حصريًا (قرار المالك `RF-014`): يوم/شهر/سنة بأربع خانات مع / أو -، وISO —
/// بلا بديل مرن (كان رهن ثقافة الخادم) وبلا سنة برقمين (كانت عتبة 2029 تجعل
/// `30` = 1930 بصمت). يعيد null لما لا يمكن تحليله.
/// </summary>
public static class ActionDateParser
{
    private static readonly string[] Formats =
    {
        "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy",
        "yyyy-MM-dd",
    };

    public static DateTime? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // يوحّد الأرقام العربية/الفارسية إلى ASCII قبل التحليل (المحلل لا يقبلها وإلا).
        value = ArabicDigitNormalizer.Normalize(value);

        // حتمي عمدًا: `InvariantCulture` + `None` (لا فراغات ولا ثقافة خادم) —
        // أي مدخل خارج الصيغ الخمس يُرفَض برسالة الحقل الودية عند المستدعي.
        if (DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            return parsed.Date;

        return null;
    }
}
