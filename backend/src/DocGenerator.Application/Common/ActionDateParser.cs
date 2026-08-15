using System.Globalization;

namespace DocGenerator.Application.Common;

/// <summary>
/// يحلّل تاريخ النص المفتوح (تواريخ القيد والإجراءات) إلى زمن حقيقي. الصيغ المعتمدة هي
/// نفسها المعتمدة في النموذج (يوم/شهر/سنة مع / أو -، وISO) مع بديل مرن عبر TryParse.
/// يعيد null لما لا يمكن تحليله (ثم يُعتمد تاريخ الإدخال في الإحصائيات).
///
/// ملاحظة قيد: البديل المرن (DateTime.TryParse بالثقافة الحالية) لا يمكن محاكاته في SQL
/// أثناء هجرة backfill الخاصة بـ DocumentRegistrationDate.DateParsed، فأي نص قديم بصيغة
/// حرة خارج الصيغ السبعة يُترك DateParsed=null وتُحسب إحصائياته من تاريخ الإدخال CreatedAt.
/// </summary>
public static class ActionDateParser
{
    private static readonly string[] Formats =
    {
        "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy",
        "yyyy-MM-dd", "d/M/yy", "dd/MM/yy",
    };

    public static DateTime? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // يوحّد الأرقام العربية/الفارسية إلى ASCII قبل التحليل (المحلل لا يقبلها وإلا).
        value = ArabicDigitNormalizer.Normalize(value);

        if (DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            return parsed.Date;

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var loose))
            return loose.Date;

        return null;
    }
}
