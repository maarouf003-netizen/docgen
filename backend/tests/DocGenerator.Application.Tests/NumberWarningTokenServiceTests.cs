using DocGenerator.Application.Common;
using DocGenerator.Infrastructure.Security;

namespace DocGenerator.Application.Tests;

public class NumberWarningTokenServiceTests
{
    private static NumberWarningTokenService Create() =>
        new(new JwtOptions { Secret = "test-secret-with-at-least-32-bytes-0123456789" });

    [Fact]
    public void Token_RoundTrips_ForSameUserDocumentFingerprintAndRows()
    {
        var service = Create();
        var token = service.Issue(7, 11, "circuit|type|year|number", new[] { 3, 1, 2 });

        Assert.True(service.Validate(token, 7, 11, "circuit|type|year|number", new[] { 2, 1, 3 }));
    }

    [Fact]
    public void Token_IsRejected_ForAnotherDocumentOrUserOrFingerprint()
    {
        var service = Create();
        var token = service.Issue(7, 11, "fp", new[] { 1 });

        Assert.False(service.Validate(token, 7, 12, "fp", new[] { 1 }));
        Assert.False(service.Validate(token, 8, 11, "fp", new[] { 1 }));
        Assert.False(service.Validate(token, 7, 11, "other", new[] { 1 }));
        Assert.False(service.Validate(token, 7, 11, "fp", new[] { 2 }));
        Assert.False(service.Validate("tampered." + token, 7, 11, "fp", new[] { 1 }));
    }
}
