using DocGenerator.Api.Security;
using Microsoft.AspNetCore.HttpsPolicy;

namespace DocGenerator.Api.Tests;

/// <summary>
/// W0-CSP: فصل <c>preload</c> عن تفعيل <c>HSTS</c> وإتاحة المدة صراحةً. الحارس هنا على
/// <b>النسخ والتحقق</b> لا على وجود إعدادات صحيحة فقط: ترويسة <c>preload</c> التزام شبه دائم
/// في قوائم المتصفحات، فلا يجوز أن تُفعَّل ضمنيًا لأن مالكًا فعّل <c>HSTS</c>.
/// </summary>
public class HstsSetupTests
{
    private static HstsOptions Configured(SecurityOptions security)
    {
        var hsts = new HstsOptions();
        HstsSetup.Configure(hsts, security);
        return hsts;
    }

    [Fact]
    public void Defaults_AreOneYearWithoutPreload()
    {
        var hsts = Configured(new SecurityOptions { HstsEnabled = true });

        Assert.Equal(TimeSpan.FromDays(365), hsts.MaxAge);
        Assert.True(hsts.IncludeSubDomains);
        Assert.False(hsts.Preload);
    }

    [Fact]
    public void MaxAge_IsCopiedFromConfiguration()
    {
        var hsts = Configured(new SecurityOptions { HstsEnabled = true, HstsMaxAgeDays = 180 });

        Assert.Equal(TimeSpan.FromDays(180), hsts.MaxAge);
    }

    [Fact]
    public void Preload_IsOptedIntoExplicitly()
    {
        var hsts = Configured(new SecurityOptions
        {
            HstsEnabled = true,
            HstsMaxAgeDays = 730,
            HstsPreload = true,
        });

        Assert.True(hsts.Preload);
        Assert.True(hsts.IncludeSubDomains);
        Assert.Equal(TimeSpan.FromDays(730), hsts.MaxAge);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveMaxAge_IsRejectedAtStartup(int days)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Configured(new SecurityOptions { HstsEnabled = true, HstsMaxAgeDays = days }));

        Assert.Contains("HstsMaxAgeDays", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreloadWithoutEnabled_IsRejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => HstsSetup.Validate(new SecurityOptions { HstsEnabled = false, HstsPreload = true }));

        Assert.Contains("HstsEnabled", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AcceptsDisabledDefaults()
    {
        HstsSetup.Validate(new SecurityOptions());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(364)]
    public void PreloadWithLessThanOneYear_IsRejected(int days)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Configured(new SecurityOptions
            {
                HstsEnabled = true,
                HstsMaxAgeDays = days,
                HstsPreload = true,
            }));

        Assert.Contains("HstsPreload", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreloadAtExactlyOneYear_IsAccepted()
    {
        var hsts = Configured(new SecurityOptions
        {
            HstsEnabled = true,
            HstsMaxAgeDays = HstsSetup.PreloadMinimumMaxAgeDays,
            HstsPreload = true,
        });

        Assert.True(hsts.Preload);
    }
}
