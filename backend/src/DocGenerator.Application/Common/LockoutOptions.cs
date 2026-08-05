namespace DocGenerator.Application.Common;

/// <summary>
/// إعدادات قفل الحساب بعد عدد محدد من محاولات الدخول الفاشلة على مستوى الحساب
/// (قابلة للضبط عبر قسم Lockout في appsettings).
/// </summary>
public class LockoutOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
