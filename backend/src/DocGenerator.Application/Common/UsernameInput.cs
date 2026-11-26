using System.Globalization;
using System.Text;

namespace DocGenerator.Application.Common;

/// <summary>
/// تطبيع مدخلات اسم المستخدم: يحذف محارف التنسيق/التحكم غير المرئية (خصوصًا محارف
/// اتجاه النص مثل U+2066…U+2069 وU+200B…U+200F وU+FEFF) التي تُلتصق عند النسخ من
/// واجهات RTL فتفشل المطابقة رغم أن الاسم «يبدو» صحيحًا. لا يغيّر الحروف/الأرقام.
/// </summary>
public static class UsernameInput
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is UnicodeCategory.Format or UnicodeCategory.Control)
                continue;
            builder.Append(character);
        }

        return builder.ToString().Trim();
    }
}
