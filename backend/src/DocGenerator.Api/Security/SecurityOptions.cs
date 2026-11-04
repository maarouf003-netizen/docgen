namespace DocGenerator.Api.Security;

/// <summary>
/// رايات الحماية لكل بيئة (قسم <c>Security</c> في الإعدادات): الافتراضي آمن لمنصة تجريبية
/// تتولى <c>TLS</c> بنفسها (<c>Render</c>)، والمخدّم الخاص يفعّل عبر متغيرات البيئة — انظر
/// <c>RUN_GUIDE.md</c> §9 وقسم «إعدادات الحماية للمخدّم الخاص».
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>عناوين <c>IP</c> البروكسي العكسي أو نطاقات <c>CIDR</c> الموثوقة. فارغ = مغلق ضد التزوير.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>تفعيل <c>HSTS</c> (سنة + <c>includeSubDomains</c> + <c>preload</c>) — المخدّم الخاص فقط.</summary>
    public bool HstsEnabled { get; set; }

    /// <summary>إعادة توجيه <c>HTTP-&gt;HTTPS</c> داخل التطبيق — المخدّم الخاص فقط (المنصات تتولاها).</summary>
    public bool HttpsRedirectionEnabled { get; set; }

    /// <summary>وضع مراقبة <c>CSP</c> (ترويسة <c>Report-Only</c>) قبل الفرض — <c>true</c> افتراضيًا.</summary>
    public bool CspReportOnly { get; set; } = true;

    /// <summary>
    /// عنوان استقبال تقارير <c>CSP</c> (توجيه <c>report-uri</c>) — فارغ = بلا جمع مركزي.
    /// يُلحَق بالسياسة فقط إن كان مسارًا/رابطًا نظيفًا (بلا بياض ولا محارف تحكم) لمنع حقن الترويسات.
    /// </summary>
    public string? CspReportUri { get; set; }

    /// <summary>
    /// سماح صريح بالتشغيل في بيئة <c>Development</c> خارج أدوات التطوير المحلية. الافتراضي
    /// <c>false</c> لأن بيئة التطوير تُعطّل خمسة ضوابط دفعةً واحدة: تطهير رسائل الاستثناء
    /// (<c>GlobalExceptionHandler</c>)، والترحيل التلقائي عند الإقلاع، و<c>Swagger</c>،
    /// و<c>CORS</c> لأصول <c>localhost</c> مع <c>AllowCredentials</c>، و<c>Secure</c> على كوكي
    /// <c>CSRF</c>. تشغيلها على خادم منشور يفتحها كلها بمتغيّر بيئة واحد، فتُشترط راية صريحة
    /// من <c>launchSettings</c>/<c>user-secrets</c>/متغيّر بيئة — ولا تُكتب في
    /// <c>appsettings.Development.json</c> لأنها تُحمَّل في البيئة المحمية نفسها فتُلغي الحارس.
    /// </summary>
    public bool AllowDevelopmentEnvironment { get; set; }
}
