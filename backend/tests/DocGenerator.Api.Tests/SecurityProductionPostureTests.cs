using DocGenerator.Api.Auth;
using DocGenerator.Api.Middleware;
using DocGenerator.Api.Security;
using DocGenerator.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس الوضع الأمني الإنتاجي (`P2-03`): رفض الإقلاع عند راية ناقصة/غير آمنة في
/// <c>Production</c>، وعدم تقييد البيئات الأخرى، وسياسة الكوكيز في الإنتاج.
/// </summary>
[Trait("Category", "Unit")]
public class SecurityProductionPostureTests
{
    [Fact]
    public void Production_WithDefaults_RefusesAndNamesEveryMissingKey()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityProductionPosture.Validate(
                HostEnvironment("Production"), Configuration(), new SecurityOptions()));

        var message = exception.Message;
        Assert.Contains("AllowedHosts", message, StringComparison.Ordinal);
        Assert.Contains("Security:HstsEnabled", message, StringComparison.Ordinal);
        Assert.Contains("Security:HttpsRedirectionEnabled", message, StringComparison.Ordinal);
        Assert.Contains("Security:CspReportOnly", message, StringComparison.Ordinal);
        Assert.Contains("Security:KnownProxies", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_WithCompliantSettings_Passes()
    {
        SecurityProductionPosture.Validate(
            HostEnvironment("Production"),
            Configuration(("AllowedHosts", "docgen.gov.sy;127.0.0.1")),
            Compliant());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void NonProduction_WithInsecureSettings_Passes(string environmentName)
    {
        SecurityProductionPosture.Validate(
            HostEnvironment(environmentName),
            Configuration(),
            new SecurityOptions());
    }

    [Fact]
    public void Production_WithWildcardAllowedHosts_Refuses()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityProductionPosture.Validate(
                HostEnvironment("Production"),
                Configuration(("AllowedHosts", "*")),
                Compliant()));

        Assert.Contains("AllowedHosts", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_WithoutTrustedProxyButWithAppTls_Passes()
    {
        var security = Compliant();
        security.KnownProxies = [];
        security.TlsTerminatedByApp = true;

        SecurityProductionPosture.Validate(
            HostEnvironment("Production"),
            Configuration(("AllowedHosts", "docgen.gov.sy")),
            security);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    public void Production_TrustingEveryProxyAddress_Refuses(string proxy)
    {
        var security = Compliant();
        security.KnownProxies = [proxy];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityProductionPosture.Validate(
                HostEnvironment("Production"),
                Configuration(("AllowedHosts", "docgen.gov.sy")),
                security));

        Assert.Contains("must not trust every address", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_WithUnparseableProxy_Refuses()
    {
        var security = Compliant();
        security.KnownProxies = ["proxy.internal"];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityProductionPosture.Validate(
                HostEnvironment("Production"),
                Configuration(("AllowedHosts", "docgen.gov.sy")),
                security));

        // ForwardedHeaders تتجاهل الإدخال غير الصالح بصمت؛ الحارس لا يسمح بذلك.
        Assert.Contains("no valid IP/CIDR", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_WithValidCidrProxy_Passes()
    {
        var security = Compliant();
        security.KnownProxies = ["10.0.0.0/8"];

        SecurityProductionPosture.Validate(
            HostEnvironment("Production"),
            Configuration(("AllowedHosts", "docgen.gov.sy")),
            security);
    }

    [Fact]
    public void Production_WithSwaggerEnabled_Refuses()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecurityProductionPosture.Validate(
                HostEnvironment("Production"),
                Configuration(("AllowedHosts", "docgen.gov.sy"), ("Swagger:Enabled", "true")),
                Compliant()));

        Assert.Contains("Swagger:Enabled", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_WithUnknownSwaggerKey_Passes()
    {
        // المفتاح الغائب يعني `false` افتراضيًا — لا يُفشل الإقلاع بلا سبب.
        SecurityProductionPosture.Validate(
            HostEnvironment("Production"),
            Configuration(("AllowedHosts", "docgen.gov.sy")),
            Compliant());
    }

    [Fact]
    public void AuthCookie_Production_IsSecureHttpOnlyStrict()
    {
        var options = AuthCookie.BuildOptions(HostEnvironment("Production"), Jwt());

        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Strict, options.SameSite);
        Assert.Equal("/", options.Path);
    }

    [Fact]
    public void AuthCookie_Development_RelaxesSecureCookieOnly()
    {
        // التنازل محصور بـDevelopment، و`Security:AllowDevelopmentEnvironment` يمنع
        // تشغيل بيئة التطوير على خادم منشور (انظر حارس H-02 في Program.cs).
        var options = AuthCookie.BuildOptions(HostEnvironment("Development"), Jwt());

        Assert.True(options.HttpOnly);
        Assert.False(options.Secure);
        Assert.Equal(SameSiteMode.Strict, options.SameSite);
    }

    [Fact]
    public void CsrfCookie_Production_IsSecureStrictAndReadable()
    {
        var options = CsrfMiddleware.CookieOptions(HostEnvironment("Production"), TimeSpan.FromHours(8));

        // HttpOnly=false مقصود: الواجهة تقرأ القيمة وتعيدها في الترويسة.
        Assert.False(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Strict, options.SameSite);
        Assert.Equal("/", options.Path);
    }

    private static SecurityOptions Compliant() => new()
    {
        HstsEnabled = true,
        HttpsRedirectionEnabled = true,
        CspReportOnly = false,
        KnownProxies = ["127.0.0.1"],
    };

    private static JwtOptions Jwt() => new() { Secret = new string('x', 64), ExpiryMinutes = 480 };

    private static TestEnvironment HostEnvironment(string name) => new() { EnvironmentName = name };

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "DocGenerator.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
