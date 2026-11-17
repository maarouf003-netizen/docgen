using Microsoft.AspNetCore.Hosting;

namespace DocGenerator.Api.Tests;

/// <summary>
/// وضع أمني إنتاجي مكتمل لمضيفات الاختبار التي تبني بيئة <c>Production</c>: منذ
/// <c>P2-03</c> يرفض <c>SecurityProductionPosture</c> الإقلاع بغيره. كل مضيف اختباري
/// يضيف احتياجاته الخاصة فوق هذه القيم، ولا يكرّرها.
/// </summary>
public static class ProductionSecurityBaseline
{
    public const string DefaultAllowedHosts = "localhost;127.0.0.1";

    public static IWebHostBuilder UseSecureProductionDefaults(
        this IWebHostBuilder builder,
        string allowedHosts = DefaultAllowedHosts,
        string proxy = "127.0.0.1") =>
        builder
            .UseSetting("AllowedHosts", allowedHosts)
            .UseSetting("Security:HstsEnabled", "true")
            .UseSetting("Security:HttpsRedirectionEnabled", "true")
            .UseSetting("Security:CspReportOnly", "false")
            .UseSetting("Security:KnownProxies:0", proxy);
}
