using DocGenerator.Application.Common;
using Xunit;

namespace DocGenerator.Application.Tests;

/// <summary>
/// W1/B5: نطاق مفتاح عدم التكرار يشمل معرّف المورد (المسار الفعلي)، وإلا أُعيدت
/// نتيجة مورد لمورد آخر بنفس المفتاح/الجسم.
/// </summary>
public sealed class IdempotencyOperationTests
{
    [Fact]
    public void Build_DifferentResourceIds_ProduceDifferentScopes()
    {
        var first = IdempotencyOperation.Build("POST", "/api/documents/1/status");
        var second = IdempotencyOperation.Build("POST", "/api/documents/2/status");

        Assert.NotEqual(first, second);
        Assert.Contains("api/documents/1/status", first);
        Assert.Contains("api/documents/2/status", second);
    }

    [Fact]
    public void Build_KeepsLegacyParameterlessRouteFormat()
    {
        // صفوف idempotency القديمة خُزّنت كـ`POST api/branches` (قالب بلا شرطة)؛
        // نُبقي الشكل نفسه للمسارات بلا معرّف كي تظل قابلة للإعادة خلال TTL.
        Assert.Equal("POST api/branches", IdempotencyOperation.Build("POST", "/api/branches"));
    }

    [Fact]
    public void Build_NormalizesMethodCaseTrailingSlashAndPathCase()
    {
        var a = IdempotencyOperation.Build("post", "/api/Documents/1/status/");
        var b = IdempotencyOperation.Build("POST", "/api/documents/1/status");

        Assert.Equal(b, a);
        Assert.StartsWith("POST ", a);
    }

    [Fact]
    public void Build_LongPath_IsHashedWithinColumnLimit()
    {
        var path = "/api/" + new string('a', 300) + "/status";

        var operation = IdempotencyOperation.Build("POST", path);

        Assert.True(operation.Length <= IdempotencyOperation.MaxLength);
        Assert.StartsWith("POST #", operation);
        Assert.Equal(operation, IdempotencyOperation.Build("POST", path));
    }
}
