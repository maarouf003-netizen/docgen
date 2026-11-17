using System.Net;
using Microsoft.Extensions.Hosting;

namespace DocGenerator.Api.Security;

/// <summary>
/// حارس <b>وضع الأمان الإنتاجي</b> (`P2-03`): يرفض إقلاع التطبيق في
/// <c>Production</c> إذا كانت أي راية حماية ما زالت على قيمتها غير الآمنة، بدل
/// تشغيل منشور بلا <c>HSTS</c>/إعادة توجيه/<c>CSP</c> صارم أو بعناوين مضيف مفتوحة.
///
/// <para>
/// <b>لماذا رفض الإقلاع لا تحذير:</b> التحذير في السجل يُقرأ مرة ثم يُنسى، ويبقى
/// المنشور يعمل بلا الحماية — وهذا بالضبط ما تمنعه `P2-03`. القرار هنا **fail-closed**
/// مقصود: إن لم تُضبط القيم في نافذة النشر فالخدمة لا تقلع (انقطاع مقصود أرخص من
/// انقطاع صامت للحماية). المرجع التشغيلي: <c>RUN_GUIDE.md</c> §9.
/// </para>
///
/// <para>
/// <b>لماذا قائمة انتهاكات مجمّعة:</b> نشر بمفاتيح ناقصة عدة مرات متتالية مضيعة؛
/// الرسالة تسمّي كل مفتاح ناقص مرة واحدة.
/// </para>
/// </summary>
public static class SecurityProductionPosture
{
    /// <summary>إشارة التوثيق التي تُلحَق برسالة الرفض.</summary>
    public const string DocsHint = "See RUN_GUIDE.md §9 (production security settings).";

    /// <summary>
    /// يتحقق فقط في <c>Production</c>؛ باقي البيئات (التطوير/الاختبار/Staging) لا تُقيَّد
    /// كي يبقى التشغيل المحلي ممكنًا، وتُغطى تنبيهاتها في <c>Program.cs</c>.
    /// </summary>
    public static void Validate(
        IHostEnvironment environment,
        IConfiguration configuration,
        SecurityOptions security)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(security);

        if (!environment.IsProduction())
            return;

        var violations = new List<string>();

        var allowedHosts = configuration["AllowedHosts"];
        var hostEntries = (allowedHosts ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hostEntries.Length == 0 || hostEntries.Contains("*", StringComparer.Ordinal))
        {
            violations.Add(
                "AllowedHosts must list the public host name(s) explicitly (for example "
                + "\"docgen.gov.sy;127.0.0.1\"); '*' disables host filtering.");
        }

        if (!security.HstsEnabled)
            violations.Add("Security:HstsEnabled must be true so browsers are pinned to HTTPS.");

        if (!security.HttpsRedirectionEnabled)
            violations.Add("Security:HttpsRedirectionEnabled must be true so plain HTTP is redirected.");

        if (security.CspReportOnly)
        {
            violations.Add(
                "Security:CspReportOnly must be false: the policy is enforced in production, "
                + "not merely reported.");
        }

        if (configuration.GetValue<bool>("Swagger:Enabled"))
        {
            violations.Add(
                "Swagger:Enabled must be false: the Swagger UI does not satisfy the enforced CSP "
                + "and exposes the API surface.");
        }

        if (!security.TlsTerminatedByApp)
        {
            var proxies = security.KnownProxies
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry.Trim())
                .ToArray();
            var validProxies = proxies.Where(IsProxyEntry).ToArray();

            if (proxies.Length == 0)
            {
                violations.Add(
                    "Security:KnownProxies must list the reverse proxy address(es)/CIDR — without them "
                    + "X-Forwarded-Proto is ignored and HTTPS redirection loops — or set "
                    + "Security:TlsTerminatedByApp=true when Kestrel terminates TLS itself.");
            }
            else if (validProxies.Length == 0)
            {
                violations.Add(
                    "Security:KnownProxies contains no valid IP/CIDR entry; ForwardedHeaders ignores "
                    + "unparseable values silently, so the app would still appear to be behind plain HTTP.");
            }
            else if (validProxies.Length < proxies.Length)
            {
                violations.Add(
                    "Security:KnownProxies contains entries that are not valid IP/CIDR; "
                    + "ForwardedHeaders ignores them silently.");
            }

            if (validProxies.Any(TrustsEveryAddress))
            {
                violations.Add(
                    "Security:KnownProxies must not trust every address (0.0.0.0/0 or ::/0): "
                    + "X-Forwarded-* would become client-controlled.");
            }
        }

        if (violations.Count == 0)
            return;

        throw new InvalidOperationException(
            "Refusing to start in Production — insecure security posture:"
            + Environment.NewLine + "- " + string.Join(Environment.NewLine + "- ", violations)
            + Environment.NewLine + DocsHint);
    }

    /// <summary>إدخال بروكسي صالح: عنوان <c>IP</c> مجرّد أو شبكة <c>CIDR</c> ببادئة صحيحة.</summary>
    private static bool IsProxyEntry(string entry)
    {
        var slash = entry.IndexOf('/');
        if (slash < 0)
            return IPAddress.TryParse(entry, out _);

        if (slash == 0 || slash == entry.Length - 1)
            return false;

        return IPAddress.TryParse(entry[..slash], out var network)
            && int.TryParse(entry[(slash + 1)..], out var prefix)
            && prefix >= 0
            && prefix <= network.GetAddressBytes().Length * 8;
    }

    /// <summary>
    /// هل الإدخال شبكة تثق بكل العناوين؟ (<c>0.0.0.0/0</c> أو <c>::/0</c>) — تزوير
    /// <c>X-Forwarded-For</c> يصبح متاحًا لأي عميل، فتُرفض في الإنتاج.
    /// </summary>
    private static bool TrustsEveryAddress(string entry)
    {
        var slash = entry.IndexOf('/');
        if (slash <= 0 || slash == entry.Length - 1)
            return false;

        if (!IPAddress.TryParse(entry[..slash], out var address))
            return false;
        if (!int.TryParse(entry[(slash + 1)..], out var prefix))
            return false;

        return prefix == 0 && (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any));
    }
}
