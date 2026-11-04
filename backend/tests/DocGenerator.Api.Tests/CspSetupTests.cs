using DocGenerator.Api.Security;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تحقق إقلاع <c>Security:CspReportUri</c> (W1): القيمة الصالحة الوحيدة هي المسار المدمج
/// <c>/api/csp-report</c>، وأي قيمة أخرى تمنع الإقلاع برسالة تسمّي المفتاح والمثال بدل
/// التجاهل الصامت أو توجيه التقارير إلى مسار غير جامع. الفارغ = الجمع معطّل بلا خطأ.
/// </summary>
public class CspSetupTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/api/csp-report")]
    [InlineData(" /api/csp-report ")]
    public void ValidOrUnset_DoesNotThrow(string? uri)
    {
        CspSetup.Validate(new SecurityOptions { CspReportUri = uri });
    }

    [Theory]
    [InlineData("https://collector.example/csp")]
    [InlineData("http://collector.example/csp")]
    [InlineData("//collector.example/csp")]
    [InlineData("csp-report")]
    [InlineData("/csp-report")]
    [InlineData("/api/csp-report/")]
    [InlineData("/api/csp-reports")]
    [InlineData("/api/csp-report?source=csp")]
    [InlineData("/api/csp-report#frag")]
    [InlineData("/api/csp report")]
    [InlineData("/api/csp-report\r\nX-Injected: 1")]
    [InlineData("/api/csp-report\"")]
    [InlineData("/api/csp-report\\")]
    [InlineData("/api/csp-report,other")]
    [InlineData("/api/csp-report;other")]
    [InlineData("/تقرير")]
    public void Invalid_FailsStartupWithKeyNameAndExample(string uri)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => CspSetup.Validate(new SecurityOptions { CspReportUri = uri }));

        Assert.Contains("Security:CspReportUri", error.Message, StringComparison.Ordinal);
        Assert.Contains(CspReportUriPolicy.Example, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TooLong_FailsStartup()
    {
        var uri = "/api/csp-report" + new string('a', CspReportUriPolicy.MaximumLength);

        var error = Assert.Throws<InvalidOperationException>(
            () => CspSetup.Validate(new SecurityOptions { CspReportUri = uri }));

        Assert.Contains(CspReportUriPolicy.MaximumLength.ToString(), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CspSetup.Validate(null!));
    }
}
