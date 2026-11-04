using System.Net;
using DocGenerator.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace DocGenerator.Api.Tests;

/// <summary>
/// إعداد <c>ForwardedHeaders</c> الموثوق (S2 + H-02): بلا وكيل معلَن **لا ثقة بأي وكيل** —
/// تُصفَّر افتراضيات الإطار (<c>::1</c> و<c>127.0.0.0/8</c>) وإلا زوّر أي عميل محلي
/// <c>X-Forwarded-For</c> فتجاوز مفتاح خنق الدخول (<c>IP:user</c>). والمدخلات الصالحة
/// (عنوان <c>IP</c> أو نطاق <c>CIDR</c> ضمن طول البادئة) تُضاف، والفاسدة تُتجاهَل بصمت.
/// </summary>
public class ForwardedHeadersOptionsTests
{
    private static ForwardedHeadersOptions Configure(params string[]? known)
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(options, known);
        return options;
    }

    [Fact]
    public void EmptyKnownProxies_TrustsNothing()
    {
        foreach (var known in new string[]?[] { null, [], ["", "   "] })
        {
            var options = Configure(known!);

            // التعطيل الصريح لا «قائمة فارغة»: الإطار يفسّر القائمتين الفارغتين كـ«ثق بكل المصادر».
            Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
            Assert.Empty(options.KnownProxies);
            Assert.Empty(options.KnownIPNetworks);
        }
    }

    [Fact]
    public void ForwardLimit_IsSingleProxyHop()
    {
        Assert.Equal(1, Configure([]).ForwardLimit);
    }

    [Fact]
    public void SingleIp_IsTrusted()
    {
        var options = Configure("10.0.0.5");

        Assert.Equal(IPAddress.Parse("10.0.0.5"), Assert.Single(options.KnownProxies));
        Assert.Empty(options.KnownIPNetworks);
    }

    [Fact]
    public void CidrRange_IsTrustedAsNetwork()
    {
        var options = Configure("10.0.0.0/8");

        Assert.Empty(options.KnownProxies);
        Assert.Single(options.KnownIPNetworks);
    }

    [Fact]
    public void InvalidEntries_AreIgnoredWithoutThrowing()
    {
        // آخر مدخلين يمرّان تحليل العنوان/البادئة ثم يفشلان في حدّ الطول:
        // البادئة 33 تتجاوز 32 لـ IPv4، و 129 تتجاوز 128 لـ IPv6.
        var options = Configure(
            "not-an-ip", "10.0.0.0/", "10.0.0.0/abc", "1.2.3.4/5/6", "  ", "10.0.0.0/33", "::1/129");

        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }

    [Theory]
    [InlineData("10.0.0.5/32")]
    [InlineData("10.0.0.0/0")]
    [InlineData("::1/128")]
    [InlineData("2001:db8::/32")]
    public void BoundaryPrefixes_AreAccepted(string entry)
    {
        var options = Configure(entry);

        Assert.Empty(options.KnownProxies);
        Assert.Single(options.KnownIPNetworks);
    }

    [Fact]
    public void MixedEntries_ValidOnesKept()
    {
        var options = Configure("  10.0.0.5  ", "garbage", "192.168.0.0/16");

        Assert.Equal(IPAddress.Parse("10.0.0.5"), Assert.Single(options.KnownProxies));
        Assert.Single(options.KnownIPNetworks);
    }
}
