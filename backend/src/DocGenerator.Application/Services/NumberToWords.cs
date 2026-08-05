using System.Text;

namespace DocGenerator.Application.Services;

/// <summary>
/// تحويل الأرقام إلى كتابة عربية — منقول من التطبيق الأصلي (number_to_arabic_words).
/// </summary>
public static class NumberToWords
{
    private static readonly string[] Ones =
        { "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة" };

    private static readonly string[] Tens =
        { "", "عشرة", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون" };

    private static readonly string[] Hundreds =
        { "", "مائة", "مائتان", "ثلاثمائة", "أربعمائة", "خمسمائة", "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة" };

    private static readonly string[] Teens =
        { "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر" };

    public static string Convert(long number)
    {
        if (number < 0)
            return "ناقص " + Convert(-number);
        if (number == 0)
            return "صفر";

        var sb = new StringBuilder();
        if (number >= 1_000_000_000)
        {
            long bl = number / 1_000_000_000;
            number %= 1_000_000_000;
            sb.Append(Millions(bl, "مليار", "ملياران"));
        }
        if (number >= 1_000_000)
        {
            long ml = number / 1_000_000;
            number %= 1_000_000;
            sb.Append(Millions(ml, "مليون", "مليونان"));
        }
        if (number >= 1000)
        {
            long th = number / 1000;
            number %= 1000;
            string tt = th switch
            {
                1 => "ألف",
                2 => "ألفان",
                _ => th is >= 3 and <= 10 ? $"{Convert(th)} آلاف" : $"{Convert(th)} ألف"
            };
            AppendUnit(sb, tt);
        }
        if (number > 0)
            AppendUnit(sb, UnderThousand((int)number));

        return sb.ToString();
    }

    private static string Millions(long value, string one, string two)
        => value switch
        {
            1 => one,
            2 => two,
            _ => $"{Convert(value)} {one}"
        };

    private static void AppendUnit(StringBuilder sb, string unit)
    {
        if (sb.Length > 0)
            sb.Append(" و");
        sb.Append(unit);
    }

    private static string UnderThousand(int number)
    {
        if (number < 10)
            return Ones[number];
        if (number < 20)
            return Teens[number - 10];
        if (number < 100)
        {
            int tp = number / 10;
            int op = number % 10;
            return op == 0 ? Tens[tp] : $"{Ones[op]} و{Tens[tp]}";
        }

        int hp = number / 100;
        int rem = number % 100;
        return rem == 0 ? Hundreds[hp] : $"{Hundreds[hp]} و{UnderThousand(rem)}";
    }
}
