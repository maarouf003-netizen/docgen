using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using DocGenerator.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// مجموعة تتابعية لاختبارات تتشارك حالة ثابتة (مثل _lastPruneUtc في DbLoginRateLimiter)
/// حتى لا تتداخل مع اختبارات متوازية.
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public sealed class SequentialCollection { }

public class TokenServiceTests
{
    [Theory]
    [InlineData(UserRole.Admin, "admin")]
    [InlineData(UserRole.Manager, "manager")]
    [InlineData(UserRole.Head, "head")]
    [InlineData(UserRole.Lawyer, "lawyer")]
    public void CreateToken_RoleClaim_IsLowercase(UserRole role, string expected)
    {
        var service = new TokenService(new JwtOptions
        {
            Secret = "test-secret-key-0123456789-0123456789-0123456789",
            Issuer = "Test",
            Audience = "Test",
        });

        var token = service.CreateToken(new User { Id = 1, Username = "u", FullName = "x", Role = role });
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(expected, jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void CreateToken_IncludesBranchId_WhenPresent()
    {
        var service = new TokenService(new JwtOptions
        {
            Secret = "test-secret-key-0123456789-0123456789-0123456789",
            Issuer = "Test",
            Audience = "Test",
        });

        var token = service.CreateToken(new User { Id = 1, Username = "u", FullName = "x", Role = UserRole.Head, BranchId = 3 });
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("3", jwt.Claims.First(c => c.Type == "branch_id").Value);
    }

    [Fact]
    public void CreateToken_IncludesTokenVersionClaim()
    {
        var service = new TokenService(new JwtOptions
        {
            Secret = "test-secret-key-0123456789-0123456789-0123456789",
            Issuer = "Test",
            Audience = "Test",
        });

        var token = service.CreateToken(new User { Id = 1, Username = "u", FullName = "x", Role = UserRole.Lawyer, TokenVersion = 7 });
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("7", jwt.Claims.First(c => c.Type == "token_version").Value);
    }
}

public class PasswordHasherLegacyTests
{
    [Fact]
    public void Verify_WerkzeugPbkdf2Hash_Succeeds()
    {
        // صيغة werkzeug المستخدمة في نسخة Flask: pbkdf2:sha256:600000$salt$hash (base64)
        var hasher = new PasswordHasher();
        var salt = Convert.ToBase64String(new byte[16] {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
        var derived = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            "123456",
            Convert.FromBase64String(salt),
            600_000,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            32);
        var hash = Convert.ToBase64String(derived);

        Assert.True(hasher.Verify("123456", $"pbkdf2:sha256:600000${salt}${hash}"));
        Assert.False(hasher.Verify("wrong", $"pbkdf2:sha256:600000${salt}${hash}"));
    }

    [Fact]
    public void Verify_MalformedWerkzeugHash_ReturnsFalse()
    {
        var hasher = new PasswordHasher();
        Assert.False(hasher.Verify("x", "pbkdf2:sha256:600000$notb64$notb64"));
        Assert.False(hasher.Verify("x", "pbkdf2:sha256:abc$c2Fsc3Q=$c2Fsc3Q="));
    }
}

[Collection("Sequential")]
public class LoginRateLimiterTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;

    public LoginRateLimiterTests()
    {
        _db = TestDb.Create();
    }

    public void Dispose() => _db.Dispose();

    private DbLoginRateLimiter Create(int max = 5, int windowMinutes = 5) => new(
        _db,
        Microsoft.Extensions.Options.Options.Create(new RateLimitOptions
        {
            MaxLoginAttempts = max,
            WindowMinutes = windowMinutes,
        }));

    [Fact]
    public async Task Allows_UpToMaxAttempts()
    {
        var limiter = Create(max: 3);
        Assert.True(await limiter.IsAllowedAsync("k"));
        await limiter.RecordFailureAsync("k");
        Assert.True(await limiter.IsAllowedAsync("k"));
        await limiter.RecordFailureAsync("k");
        Assert.True(await limiter.IsAllowedAsync("k"));
        await limiter.RecordFailureAsync("k");
        Assert.False(await limiter.IsAllowedAsync("k"));
    }

    [Fact]
    public async Task Reset_ClearsFailures()
    {
        var limiter = Create(max: 1);
        await limiter.RecordFailureAsync("k");
        Assert.False(await limiter.IsAllowedAsync("k"));
        await limiter.ResetAsync("k");
        Assert.True(await limiter.IsAllowedAsync("k"));
    }

    [Fact]
    public async Task Keys_AreIsolated()
    {
        var limiter = Create(max: 1);
        await limiter.RecordFailureAsync("a");
        Assert.True(await limiter.IsAllowedAsync("b"));
        Assert.False(await limiter.IsAllowedAsync("a"));
    }

    [Fact]
    public async Task Prune_RemovesAttemptsOlderThanWindow()
    {
        ResetPruneTimestamp();

        _db.LoginAttempts.Add(new LoginAttempt { Key = "old", AttemptedAtUtc = DateTime.UtcNow.AddMinutes(-60) });
        await _db.SaveChangesAsync();

        var limiter = Create(max: 5);
        await limiter.RecordFailureAsync("fresh");

        Assert.Equal(0, await _db.LoginAttempts.CountAsync(a => a.Key == "old"));
        Assert.Equal(1, await _db.LoginAttempts.CountAsync(a => a.Key == "fresh"));
    }

    private static void ResetPruneTimestamp()
    {
        typeof(DbLoginRateLimiter)
            .GetField("_lastPruneTicks", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, DateTime.MinValue.Ticks);
    }
}

public class HtmlInputSanitizerTests
{
    [Fact]
    public void Sanitize_RemovesScriptAndEventHandlers_KeepsPlainText()
    {
        var html = "<p onclick=\"alert(1)\">نص</p><script>evil()</script><img src=\"x\" onerror=\"alert(1)\">";

        var result = HtmlInputSanitizer.Sanitize(html);

        Assert.DoesNotContain("<script", result);
        Assert.DoesNotContain("onclick", result);
        Assert.DoesNotContain("onerror", result);
        Assert.DoesNotContain("<img", result);
        Assert.Contains("نص", result);
    }

    [Fact]
    public void Sanitize_RemovesDisallowedTags_KeepsAllowedFormatting()
    {
        var html = "<h1>عنوان</h1><p><strong>بند</strong> و<em>تشديد</em></p>";

        var result = HtmlInputSanitizer.Sanitize(html);

        Assert.DoesNotContain("<h1", result);
        Assert.Contains("<strong>بند</strong>", result);
        Assert.Contains("<em>تشديد</em>", result);
    }

    [Fact]
    public void Sanitize_KeepsOnlyColorCssProperty()
    {
        var html = "<span style=\"color:#dc2626;background:url(javascript:evil())\">نص</span>";

        var result = HtmlInputSanitizer.Sanitize(html);

        Assert.Contains("color", result);
        Assert.DoesNotContain("background", result);
        Assert.DoesNotContain("url", result);
        Assert.Contains("نص", result);
    }

    [Fact]
    public void Sanitize_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, HtmlInputSanitizer.Sanitize(null));
        Assert.Equal(string.Empty, HtmlInputSanitizer.Sanitize("   "));
    }

    [Fact]
    public void ToPlainText_ExtractsText_WithoutTags()
    {
        var result = HtmlInputSanitizer.ToPlainText("<p>إجراء <strong>هام</strong></p><p>سطر ثانٍ</p>");

        Assert.Equal("إجراء هام سطر ثانٍ", result);
    }

    [Fact]
    public void ToPlainText_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, HtmlInputSanitizer.ToPlainText(null));
        Assert.Equal(string.Empty, HtmlInputSanitizer.ToPlainText(string.Empty));
    }
}
