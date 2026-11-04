using DocGenerator.Api.Security;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تحقق إقلاع <c>Security:CspReportUri</c> (W1): القيمة الخاطئة تمنع الإقلاع برسالة
/// تسمّي المفتاح والمثال بدل التجاهل الصامت. والقيم الفارغة تعني «الجمع معطّل» بلا خطأ.
/// </summary>
public class CspSetupTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/api/csp-report")]
    [InlineData("/custom/report?source=csp")]
    public void ValidOrUnset_DoesNotThrow(string? uri)
    {
        CspSetup.Validate(new SecurityOptions { CspReportUri = uri });
    }

    [Theory]
    [InlineData("https://collector.example/csp")]
    [InlineData("http://collector.example/csp")]
    [InlineData("//collector.example/csp")]
    [InlineData("csp-report")]
    [InlineData("/csp report")]
    [InlineData("/csp-report\r\nX-Injected: 1")]
    [InlineData("/csp-report\"")]
    [InlineData("/csp-report\\")]
    [InlineData("/csp-report,other")]
    [InlineData("/csp-report;other")]
    [InlineData("/تقرير")]
    public void Invalid_FailsStartupWithKeyNameAndExample(string uri)
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => CspSetup.Validate(new SecurityOptions { CspReportUri = uri }));

        Assert.Contains("Security:CspReportUri", error.Message, StringComparison.Ordinal);
        Assert.Contains(CspReportUriPolicy.Example, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CspSetup.Validate(null!));
    }
}
