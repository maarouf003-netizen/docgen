using System.Net;
using DocGenerator.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace DocGenerator.Api.Tests;

/// <summary>
/// إعداد <c>ForwardedHeaders</c> الموثوق (S2): الإعدادات لا تمسّ افتراضيات الإطار (loopback)،
/// والمدخلات الصالحة (عنوان <c>IP</c> أو نطاق <c>CIDR</c>) تُضاف، والفاسدة تُتجاهَل بصمت.
/// </summary>
public class ForwardedHeadersOptionsTests
{
    private static ForwardedHeadersOptions Configure(params string[]? known)
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(options, known);
        return options;
    }

    private static ForwardedHeadersOptions Baseline() => new();

    [Fact]
    public void EmptyKnownProxies_AddsNothing()
    {
        foreach (var known in new string[]?[] { null, [], ["", "   "] })
        {
            var options = Configure(known!);
            var baseline = Baseline();
            Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
            Assert.Equal(baseline.KnownProxies.Count, options.KnownProxies.Count);
            Assert.Equal(baseline.KnownIPNetworks.Count, options.KnownIPNetworks.Count);
        }
    }

    [Fact]
    public void SingleIp_IsTrusted()
    {
        var options = Configure("10.0.0.5");
        Assert.Contains(IPAddress.Parse("10.0.0.5"), options.KnownProxies);
        Assert.Equal(Baseline().KnownProxies.Count + 1, options.KnownProxies.Count);
        Assert.Equal(Baseline().KnownIPNetworks.Count, options.KnownIPNetworks.Count);
    }

    [Fact]
    public void CidrRange_IsTrustedAsNetwork()
    {
        var options = Configure("10.0.0.0/8");
        Assert.Equal(Baseline().KnownProxies.Count, options.KnownProxies.Count);
        Assert.Equal(Baseline().KnownIPNetworks.Count + 1, options.KnownIPNetworks.Count);
    }

    [Fact]
    public void InvalidEntries_AreIgnoredWithoutThrowing()
    {
        var options = Configure("not-an-ip", "10.0.0.0/", "10.0.0.0/abc", "1.2.3.4/5/6", "  ");
        Assert.Equal(Baseline().KnownProxies.Count, options.KnownProxies.Count);
        Assert.Equal(Baseline().KnownIPNetworks.Count, options.KnownIPNetworks.Count);
    }

    [Fact]
    public void MixedEntries_ValidOnesKept()
    {
        var options = Configure("  10.0.0.5  ", "garbage", "192.168.0.0/16");
        Assert.Contains(IPAddress.Parse("10.0.0.5"), options.KnownProxies);
        Assert.Equal(Baseline().KnownProxies.Count + 1, options.KnownProxies.Count);
        Assert.Equal(Baseline().KnownIPNetworks.Count + 1, options.KnownIPNetworks.Count);
    }
}
