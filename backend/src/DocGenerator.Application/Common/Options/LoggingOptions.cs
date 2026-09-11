namespace DocGenerator.Application.Common.Options;

/// <summary>
/// إعدادات ملف سجل الأخطاء الدوّار (قابلة للضبط عبر قسم Logging:File في appsettings).
/// </summary>
public class LoggingOptions
{
    public string Path { get; set; } = "logs/logs-.txt";
    public int RetainedDays { get; set; } = 31;
    public long FileSizeLimitBytes { get; set; } = 50 * 1024 * 1024;
    public int ClientErrorPerMinuteLimit { get; set; } = 30;
    public int ClientErrorPayloadSizeLimit { get; set; } = 2048;
    public int ClientErrorStackLimit { get; set; } = 8192;
}
