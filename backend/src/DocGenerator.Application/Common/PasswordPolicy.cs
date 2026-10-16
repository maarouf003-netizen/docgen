namespace DocGenerator.Application.Common;

/// <summary>
/// سياسة كلمة المرور الموحدة (M-02 — قرار المالك: حد 10 + قائمة ممنوعة):
/// نقطة تحقق واحدة لكل مسارات الإنشاء/التغيير (محامٍ/مستخدم/مندوب جهة) —
/// لا تحقق مكرر ولا باب خلفي بحد أضعف. مسار الدخول لا يتحقق (يقارن البصمة فقط)
/// فتبقى كلمات البذر/الاختبار القائمة صالحة للدخول دون أن تُقبَل عند التعيين.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>الحد الأدنى لطول كلمة المرور (قرار المالك 2026-10-16).</summary>
    public const int MinLength = 10;

    /// <summary>
    /// كلمات شائعة مرفوضة حتى بطول كافٍ (مقارنة غير حساسة لحالة الأحرف).
    /// القائمة حدّ أدنى محافظ — لا تُوسَّع بكلمات حقيقية للمستخدمين.
    /// </summary>
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password12", "password123", "password1234",
        "1234567890", "0987654321", "12345678901",
        "qwerty12345", "qwertyuiop12", "letmein1234",
        "admin123456", "admin12345", "welcome12345",
        "docgen1234", "damascus123", "syria123456",
        "aaaaaaaaaa", "1111111111",
    };

    /// <summary>
    /// يعيد رسالة الخطأ العربية، أو <c>null</c> عند قبول الكلمة.
    /// يُفحص النص المقصوص (<c>Trim</c>) عمدًا حتى لا يخدع حشو المسافات فحص الطول
    /// (مثال: <c>"  123456  "</c> بطول 10 ظاهريًا وقوته الحقيقية 6).
    /// </summary>
    public static string? Validate(string? password, string? username = null)
    {
        var candidate = password?.Trim();
        if (string.IsNullOrEmpty(candidate) || candidate.Length < MinLength)
            return $"كلمة المرور يجب أن تكون {MinLength} أحرف على الأقل";
        if (CommonPasswords.Contains(candidate))
            return "كلمة المرور ضعيفة وشائعة — اختر كلمة أخرى";
        if (IsUniform(candidate))
            return "كلمة المرور ضعيفة — لا تستخدم حرفًا واحدًا مكررًا";
        if (!string.IsNullOrWhiteSpace(username)
            && string.Equals(candidate, username.Trim(), StringComparison.OrdinalIgnoreCase))
            return "كلمة المرور يجب ألا تطابق اسم الدخول";
        return null;
    }

    /// <summary>يتحقق ويرمي <see cref="ArgumentException"/> برسالة عربية عند الرفض.</summary>
    public static void EnsureValid(string? password, string? username = null)
    {
        var error = Validate(password, username);
        if (error is not null)
            throw new ArgumentException(error);
    }

    private static bool IsUniform(string password)
    {
        for (var i = 1; i < password.Length; i++)
            if (password[i] != password[0])
                return false;
        return true;
    }
}
