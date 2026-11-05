using DocGenerator.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace DocGenerator.Api.Tests;

/// <summary>
/// ترويسات الكاش لملفات الواجهة: قشرة SPA تُعيد التحقق دائمًا (وإلا بقي المتصفح
/// على حزمة قديمة بعد النشر)، والأصول المُبصَّمة تُخزَّن immutable، وغيرها بلا تغيير.
/// </summary>
public sealed class SpaCacheHeadersTests
{
    [Fact]
    public void IndexHtml_IsAlwaysRevalidated()
    {
        var context = BuildContext("/", "index.html");

        SpaCacheHeaders.Apply(context);

        Assert.Equal("no-cache, max-age=0, must-revalidate", context.Context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Context.Response.Headers.Pragma);
        Assert.Equal("0", context.Context.Response.Headers.Expires);
    }

    [Fact]
    public void SpaFallbackDeepLink_IsAlwaysRevalidated()
    {
        var context = BuildContext("/forum", "index.html");

        SpaCacheHeaders.Apply(context);

        Assert.Equal("no-cache, max-age=0, must-revalidate", context.Context.Response.Headers.CacheControl);
    }

    [Fact]
    public void HashedAsset_IsImmutable()
    {
        var context = BuildContext("/assets/index-abc123.js", "index-abc123.js");

        SpaCacheHeaders.Apply(context);

        Assert.Equal("public, max-age=31536000, immutable",
            context.Context.Response.Headers.CacheControl);
    }

    [Fact]
    public void OtherStaticFiles_KeepDefaultCaching()
    {
        var context = BuildContext("/login-hero.avif", "login-hero.avif");

        SpaCacheHeaders.Apply(context);

        Assert.True(string.IsNullOrEmpty(context.Context.Response.Headers.CacheControl));
    }

    private static StaticFileResponseContext BuildContext(string requestPath, string fileName)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = requestPath;
        return new StaticFileResponseContext(http, new FakeFileInfo(fileName));
    }

    private sealed class FakeFileInfo(string name) : IFileInfo
    {
        public bool Exists => true;
        public long Length => 0;
        public string? PhysicalPath => null;
        public string Name { get; } = name;
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => Stream.Null;
    }
}
