namespace DocGenerator.Application.Common;

/// <summary>
/// إعدادات قفل الحساب بعد عدد محدد من محاولات الدخول الفاشلة على مستوى الحساب
/// (قابلة للضبط عبر قسم Lockout في appsettings). مدة القفل تصاعدية أسّيًا (S6):
/// القفل الأول بالمدة الأساسية، وكل قفل متتالٍ دون نجاح بينهما يضاعفها حتى السقف.
/// </summary>
public class LockoutOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;

    /// <summary>سقف مدة أي قفل مفرد بالدقائق (يحدّ من النمو الأسّي).</summary>
    public int MaxLockoutMinutes { get; set; } = 120;
}
