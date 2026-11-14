namespace DocGenerator.Application.Common;

/// <summary>
/// إعدادات قاعدة البيانات (القسم <c>Database</c>) — الخطة §16.1.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>اسم القسم في الإعدادات.</summary>
    public const string SectionName = "Database";

    /// <summary>
    /// هل يطبّق التطبيق الهجرات تلقائيًا عند الإقلاع؟
    /// <c>true</c> في التطوير والاختبار فقط؛ و<c>false</c> في الإنتاج، حيث تُطبَّق
    /// الهجرات صراحةً قبل تشغيل النسخة (لا يُعتمد على الترحيل التلقائي في الإنتاج).
    /// </summary>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>
    /// عدد محاولات إعادة التنفيذ للعمليات <b>القابلة لإعادة المحاولة</b> فقط
    /// (<c>P0-03b</c>). <c>0</c> = معطّل (الافتراضي الآمن). لا يؤثر على
    /// <c>SaveChanges</c> داخل معاملة نشطة — فهي غير معيدة دائمًا.
    /// </summary>
    public int RetryMaxCount { get; set; }

    /// <summary>أقصى تأخير بين محاولات إعادة التنفيذ بالثواني.</summary>
    public int RetryMaxDelaySeconds { get; set; } = 30;

    public void Validate()
    {
        if (RetryMaxCount < 0)
            throw new InvalidOperationException("Database:RetryMaxCount must be zero or positive.");
        if (RetryMaxDelaySeconds <= 0)
            throw new InvalidOperationException("Database:RetryMaxDelaySeconds must be positive.");
    }
}
