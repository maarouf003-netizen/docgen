using DocGenerator.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace DocGenerator.Api.Tests;

/// <summary>
/// W2/B2 + W2/F13: فحص الأصل في <see cref="CsrfMiddleware"/> يمنع Login CSRF (تثبيت
/// جلسة المهاجم في متصفح الضحية) ويُطبَّق حتى على المسارات المستثناة من مطابقة التوكن،
/// مع بقاء عملاء API (بلا ترويسات متصفح) عاملين. ومطابقة المسار المستثنى تامة —
/// لا `EndsWith` يفتح أي مسار آخر ينتهي بـ`/api/auth/login`.
/// </summary>
[Trait("Category", "Unit")]
public class CsrfMiddlewareTests
{
    [Fact]
    public async Task Login_WithForeignOrigin_Returns403AndDoesNotInvokeNext()
    {
        var (nextCalled, context) = await InvokeAsync(
            "POST", "/api/auth/login",
            request => request.Headers.Origin = "https://evil.example");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Login_WithSameOrigin_InvokesNext()
    {
        var (nextCalled, context) = await InvokeAsync(
            "POST", "/api/auth/login",
            request => request.Headers.Origin = "https://app.test");

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Login_WithSameSiteFetchHeader_Returns403()
    {
        // `same-site` يشمل نطاقات فرعية قد تكون تحت سيطرة طرف آخر — لا يُقبل.
        var (nextCalled, context) = await InvokeAsync(
            "POST", "/api/auth/login",
            request => request.Headers[CsrfMiddleware.FetchSiteHeader] = "same-site");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Login_WithoutBrowserHeaders_InvokesNext()
    {
        // عملاء API/الاختبارات لا يرسلون Origin/Sec-Fetch-Site — لا يُرفضون.
        var (nextCalled, _) = await InvokeAsync("POST", "/api/auth/login");

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Logout_WithForeignOrigin_Returns403()
    {
        var (nextCalled, context) = await InvokeAsync(
            "POST", "/api/auth/logout",
            request => request.Headers.Origin = "https://evil.example");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task WriteWithValidCsrfPairButForeignOrigin_Returns403()
    {
        var (nextCalled, context) = await InvokeAsync(
            "POST", "/api/documents",
            request =>
            {
                request.Headers.Cookie = $"{CsrfMiddleware.CookieName}=pair";
                request.Headers[CsrfMiddleware.HeaderName] = "pair";
                request.Headers.Origin = "https://evil.example";
            });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task WriteWithValidCsrfPairAndNoBrowserHeaders_InvokesNext()
    {
        var (nextCalled, _) = await InvokeAsync(
            "POST", "/api/documents",
            request =>
            {
                request.Headers.Cookie = $"{CsrfMiddleware.CookieName}=pair";
                request.Headers[CsrfMiddleware.HeaderName] = "pair";
            });

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task PathEndingWithLogin_IsNotTreatedAsExempt()
    {
        // F13: المطابقة اللاحقة كانت تجعل `/evil/api/auth/login` مستثنى من توكن CSRF.
        var (nextCalled, context) = await InvokeAsync("POST", "/evil/api/auth/login");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private static async Task<(bool NextCalled, HttpContext Context)> InvokeAsync(
        string method, string path, Action<HttpRequest>? configure = null)
    {
        var nextCalled = false;
        var middleware = new CsrfMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.test");
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        configure?.Invoke(context.Request);

        await middleware.InvokeAsync(context);
        return (nextCalled, context);
    }
}
