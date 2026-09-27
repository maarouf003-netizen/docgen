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

    /// <summary>المسارات المكلفة (تصدير/إحصاءات/توليد) لكل مستخدم.</summary>
    public int ExpensivePerMinute { get; set; } = 10;

    /// <summary>تغيير كلمة المرور لكل مستخدم.</summary>
    public int PasswordPerMinute { get; set; } = 5;
}
