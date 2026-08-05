namespace DocGenerator.Application.Common;

/// <summary>
/// إعدادات تحديد المحاولات (قابلة للضبط عبر قسم RateLimiting في appsettings).
/// </summary>
public class RateLimitOptions
{
    public int MaxLoginAttempts { get; set; } = 5;
    public int WindowMinutes { get; set; } = 5;
}
