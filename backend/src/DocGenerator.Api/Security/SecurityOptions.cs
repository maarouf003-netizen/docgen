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

    /// <summary>
    /// تفعيل <c>HSTS</c> — المخدّم الخاص فقط، وبعد تأكيد أن <c>HTTPS</c> يعمل على كل المسارات
    /// (الترويسة تُلزِم المتصفح بالبروتوكول الآمن مدة <see cref="HstsMaxAgeDays"/>).
    /// </summary>
    public bool HstsEnabled { get; set; }

    /// <summary>
    /// مدة <c>HSTS</c> بالأيام (افتراضيًا سنة = 365). <c>preload</c> يشترط 365 يومًا على الأقل
    /// (متطلب قائمة المتصفحات المدمجة)، ويُرفض الإقلاع عند قيمة غير موجبة أو تتجاوز
    /// <see cref="HstsSetup.MaximumMaxAgeDays"/>.
    /// </summary>
    public int HstsMaxAgeDays { get; set; } = 365;

    /// <summary>
    /// إضافة <c>includeSubDomains</c> إلى ترويسة <c>HSTS</c> — <c>false</c> افتراضيًا لأن الترويسة
    /// تفرض <c>HTTPS</c> على كل النطاقات الفرعية، فأي نطاق فرعي يخدم عبر <c>HTTP</c> فقط ينكسر
    /// فورًا. يُفعَّل صراحةً بعد التأكد من <c>HTTPS</c> الكامل لكل النطاقات الفرعية، ويُفرَض
    /// <c>true</c> تلقائيًا عند <see cref="HstsPreload"/> (متطلب قوائم المتصفحات).
    /// </summary>
    public bool HstsIncludeSubDomains { get; set; }

    /// <summary>
    /// إدراج <c>preload</c> في ترويسة <c>HSTS</c> — <c>false</c> افتراضيًا. تفعيله التزام لا رجعة
    /// فيه تقريبًا (إزالة النطاق من قوائم المتصفحات تستغرق أشهرًا)، فلا يُفعَّل إلا على النطاق
    /// النهائي المستقر بعد سنة من <c>HTTPS</c> الكامل بما فيه النطاقات الفرعية.
    /// </summary>
    public bool HstsPreload { get; set; }

    /// <summary>إعادة توجيه <c>HTTP-&gt;HTTPS</c> داخل التطبيق — المخدّم الخاص فقط (المنصات تتولاها).</summary>
    public bool HttpsRedirectionEnabled { get; set; }

    /// <summary>وضع مراقبة <c>CSP</c> (ترويسة <c>Report-Only</c>) قبل الفرض — <c>true</c> افتراضيًا.</summary>
    public bool CspReportOnly { get; set; } = true;

    /// <summary>
    /// مسار استقبال تقارير <c>CSP</c> (يُلحَق كـ<c>report-uri</c> احتياطًا و<c>report-to</c>
    /// حديثًا، مع ترويسة <c>Reporting-Endpoints</c>) — فارغ = بلا جمع مركزي.
    /// يُقبل **مسار جذري-نسبي فقط** يبدأ بـ<c>/</c> وليس <c>//</c>، بلا بياض/تحكم/محارف
    /// تكسر الترويسة. القيمة غير الصالحة **تمنع الإقلاع** برسالة تسمّي المفتاح (<see cref="CspSetup.Validate"/>)
    /// بدل التجاهل الصامت. مثال: <c>/api/csp-report</c>.
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
