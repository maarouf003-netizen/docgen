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
}
