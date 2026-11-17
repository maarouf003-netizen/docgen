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

    /// <summary>
    /// إعدادات مجمّع اتصالات <c>Npgsql</c> (<c>Database:Pool</c>). تُطبَّق كقيم افتراضية
    /// فقط: أي مفتاح مذكور صراحةً في سلسلة الاتصال يبقى محترمًا (المشغّل أعلم بحدود المضيف).
    /// </summary>
    public DatabasePoolOptions Pool { get; set; } = new();

    public void Validate()
    {
        if (RetryMaxCount < 0)
            throw new InvalidOperationException("Database:RetryMaxCount must be zero or positive.");
        if (RetryMaxDelaySeconds <= 0)
            throw new InvalidOperationException("Database:RetryMaxDelaySeconds must be positive.");
        Pool.Validate();
    }
}

/// <summary>
/// حدود مجمّع الاتصالات (`P3-09`): تمنع استنزاف حدود <c>Postgres</c> المُدارة عند
/// التوسع الأفقي، وتضع مهلًا صريحة بدل سلوك افتراضي غامض تحت الحمل.
/// </summary>
public sealed class DatabasePoolOptions
{
    /// <summary>أقصى اتصالات لكل نسخة. الافتراضي 20.</summary>
    public int MaxPoolSize { get; set; } = 20;

    /// <summary>أدنى اتصالات محفوظة مفتوحة. `0` = لا اتصال دائم (الافتراضي).</summary>
    public int MinPoolSize { get; set; }

    /// <summary>مهلة انتظار اتصال من المجمّع بالثواني (Npgsql <c>Timeout</c>).</summary>
    public int ConnectionTimeoutSeconds { get; set; } = 15;

    /// <summary>مهلة تنفيذ الأمر بالثواني (Npgsql <c>Command Timeout</c>).</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>عمر الاتصال الخامل بالثواني قبل إغلاقه. `0` = بلا حد (غير مستحسن).</summary>
    public int IdleLifetimeSeconds { get; set; } = 300;

    /// <summary>
    /// سقف زمن فحص جهوزية قاعدة البيانات بالثواني: منصة النشر تقطع فحص الصحة عند مهلة
    /// قصيرة، فلا يجوز أن ينتظر الفحص اتصالًا من مجمّع مستنزف أطول من ذلك.
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    public void Validate()
    {
        if (MaxPoolSize is < 1 or > 200)
            throw new InvalidOperationException(
                $"Database:Pool:MaxPoolSize must be between 1 and 200 (got {MaxPoolSize}).");
        if (MinPoolSize < 0 || MinPoolSize > MaxPoolSize)
            throw new InvalidOperationException(
                $"Database:Pool:MinPoolSize must be between 0 and MaxPoolSize ({MaxPoolSize}); got {MinPoolSize}.");
        if (ConnectionTimeoutSeconds is < 1 or > 120)
            throw new InvalidOperationException(
                "Database:Pool:ConnectionTimeoutSeconds must be between 1 and 120 "
                + $"(got {ConnectionTimeoutSeconds}).");
        if (CommandTimeoutSeconds is < 1 or > 600)
            throw new InvalidOperationException(
                "Database:Pool:CommandTimeoutSeconds must be between 1 and 600 "
                + $"(got {CommandTimeoutSeconds}).");
        if (IdleLifetimeSeconds is < 0 or > 3600)
            throw new InvalidOperationException(
                "Database:Pool:IdleLifetimeSeconds must be between 0 and 3600 "
                + $"(got {IdleLifetimeSeconds}).");
        if (HealthCheckTimeoutSeconds is < 1 or > 30)
            throw new InvalidOperationException(
                "Database:Pool:HealthCheckTimeoutSeconds must be between 1 and 30 "
                + $"(got {HealthCheckTimeoutSeconds}).");
    }
}
