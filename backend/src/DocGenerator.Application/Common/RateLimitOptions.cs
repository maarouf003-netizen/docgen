namespace DocGenerator.Application.Common;

/// <summary>
/// إعدادات تحديد المعدل (قابلة للضبط عبر قسم RateLimiting في appsettings).
/// <c>MaxLoginAttempts</c>/<c>WindowMinutes</c> للمحدد القديم المعتمد على القاعدة (لكل IP+username)،
/// والبقية لسياسات <c>AddRateLimiter</c> المدمج (نافذة دقيقة واحدة لكل منها).
/// </summary>
public class RateLimitOptions
{
    public int MaxLoginAttempts { get; set; } = 5;
    public int WindowMinutes { get; set; } = 5;

    /// <summary>طبقة شبكية لكل IP على الدخول (دفاع بالعمق فوق محدد القاعدة).</summary>
    public int LoginIpPerMinute { get; set; } = 10;

    /// <summary>الحد العام لكل IP مجهول.</summary>
    public int GeneralAnonPerMinute { get; set; } = 100;

    /// <summary>الحد العام لكل مستخدم مصادَق.</summary>
    public int GeneralAuthPerMinute { get; set; } = 300;

    /// <summary>المسارات المكلفة (تصدير/توليد/استيراد جماعي) لكل مستخدم.</summary>
    public int ExpensivePerMinute { get; set; } = 10;

    /// <summary>
    /// قراءات شاشة الإحصاءات لكل مستخدم — أعلى من «المكلفة» عمدًا لأن الصفحة
    /// تطلق عدة استعلامات لكل تفاعل (فترة/فرع/سابق)، والحد 10 كان يسبب 429.
    /// </summary>
    public int StatsReadPerMinute { get; set; } = 120;

    /// <summary>
    /// قراءات دوائر التنفيذ (قوائم/نافذة إفراغ/إحصاءات) لكل مستخدم — سياسة مستقلة عن
    /// «المكلفة» لأن شاشات القوائم تطلق أكثر من قراءة لكل تفاعل، والحد 10 كان يخنق
    /// القراءة ويُسقط خيارات «دائرة التنفيذ» إلى الاحتياطي بصمت. الكتابة/التصدير يبقيان
    /// على <c>ExpensivePerMinute</c>.
    /// </summary>
    public int CircuitsReadPerMinute { get; set; } = 60;

    /// <summary>تغيير كلمة المرور لكل مستخدم.</summary>
    public int PasswordPerMinute { get; set; } = 5;

    /// <summary>
    /// تقارير <c>CSP</c> المجهولة لكل <c>IP</c> — النقطة عامة (المتصفح لا يملك جلسة/CSRF
    /// عند إرسال التقرير)، فهذا الحد يمنع إغراق السجل مع سقف حجم الجسم وعدد التقارير.
    /// </summary>
    public int CspReportPerMinute { get; set; } = 60;

    /// <summary>
    /// يرفض القيم غير الصالحة عند الإقلاع بدل تمريرها إلى <c>AddRateLimiter</c> (حيث
    /// كان <c>Math.Max(1, …)</c> يخفي الصفر/السالب بصمت فيتحول خطأ إعداد إلى حد مفعّل).
    /// </summary>
    public void Validate()
    {
        RequireRange(MaxLoginAttempts, 1, 100, "RateLimiting:MaxLoginAttempts");
        RequireRange(WindowMinutes, 1, 60, "RateLimiting:WindowMinutes");
        RequireRange(LoginIpPerMinute, 1, 1_000_000, "RateLimiting:LoginIpPerMinute");
        RequireRange(GeneralAnonPerMinute, 1, 1_000_000, "RateLimiting:GeneralAnonPerMinute");
        RequireRange(GeneralAuthPerMinute, 1, 1_000_000, "RateLimiting:GeneralAuthPerMinute");
        RequireRange(ExpensivePerMinute, 1, 1_000_000, "RateLimiting:ExpensivePerMinute");
        RequireRange(StatsReadPerMinute, 1, 1_000_000, "RateLimiting:StatsReadPerMinute");
        RequireRange(CircuitsReadPerMinute, 1, 1_000_000, "RateLimiting:CircuitsReadPerMinute");
        RequireRange(PasswordPerMinute, 1, 1_000_000, "RateLimiting:PasswordPerMinute");
        RequireRange(CspReportPerMinute, 1, 1_000_000, "RateLimiting:CspReportPerMinute");
    }

    private static void RequireRange(int value, int min, int max, string key)
    {
        if (value < min || value > max)
        {
            throw new InvalidOperationException(
                $"{key} must be between {min} and {max} (got {value}).");
        }
    }
}
