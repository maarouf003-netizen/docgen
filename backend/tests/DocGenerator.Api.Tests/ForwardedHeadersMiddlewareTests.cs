using System.Net;
using DocGenerator.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// H-02b: الخاصية الأمنية الفعلية — لا يكفي أن تكون القوائم صحيحة في الإعدادات، بل أن
/// <c>ForwardedHeadersMiddleware</c> يتجاهل الترويسات من عميل غير مصرَّح به. الحالة الحرجة هي
/// الافتراض القديم: `loopback` موثوق تلقائيًا ⇒ عملية محلية تزوّر <c>X-Forwarded-For</c> فتتجاوز
/// مفتاح خنق الدخول (<c>IP:user</c>). هذه الاختبارات تفشل لو عادت الافتراضيات يومًا.
/// </summary>
public class ForwardedHeadersMiddlewareTests
{
    private static ForwardedHeadersOptions Configured(params string[] known)
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(options, known);
        return options;
    }

    private static DefaultHttpContext Context(string remoteIp, string forwardedFor, string forwardedProto)
    {
        var context = new DefaultHttpContext();
        // `DefaultHttpContext` لا يفترض مخططًا (`Scheme` فارغ)، بينما الطلب الحقيقي دائمًا `http`
        // أو `https` — يُثبَّت هنا ليكون «لم يتغيّر» معنى قابلًا للقياس.
        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
        return context;
    }

    private static ForwardedHeadersMiddleware Middleware(ForwardedHeadersOptions options)
        => new(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

    [Fact]
    public async Task TrustedProxy_AppliesForwardedForAndProto()
    {
        var context = Context("10.0.0.5", "203.0.113.9", "https");

        await Middleware(Configured("10.0.0.5")).Invoke(context);

        Assert.Equal(IPAddress.Parse("203.0.113.9"), context.Connection.RemoteIpAddress);
        Assert.Equal("https", context.Request.Scheme);
    }

    [Fact]
    public async Task UntrustedClient_IsIgnored()
    {
        var context = Context("203.0.113.7", "10.1.1.1", "https");

        await Middleware(Configured("10.0.0.5")).Invoke(context);

        Assert.Equal(IPAddress.Parse("203.0.113.7"), context.Connection.RemoteIpAddress);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Fact]
    public async Task EmptyConfiguration_IgnoresLoopbackSpoofing()
    {
        // العملية المحلية كانت موثوقة افتراضيًا قبل H‑02b: هذا الاختبار يثبّت إغلاق ذلك.
        var context = Context("127.0.0.1", "203.0.113.9", "https");

        await Middleware(Configured()).Invoke(context);

        Assert.Equal(IPAddress.Loopback, context.Connection.RemoteIpAddress);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Fact]
    public async Task CidrNetwork_IsTrusted()
    {
        var context = Context("10.1.2.3", "203.0.113.9", "https");

        await Middleware(Configured("10.0.0.0/8")).Invoke(context);

        Assert.Equal(IPAddress.Parse("203.0.113.9"), context.Connection.RemoteIpAddress);
    }
}
