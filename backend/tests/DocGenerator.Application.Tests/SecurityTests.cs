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

public class PasswordHasherV1Tests
{
    private const string V1Prefix = "$docgen$v1$";

    [Fact]
    public void Hash_ProducesSelfDescribingV1Format_AndRoundTrips()
    {
        var hasher = new PasswordHasher();

        var hash = hasher.Hash("123456");

        Assert.StartsWith(V1Prefix, hash);
        var segments = hash[V1Prefix.Length..].Split('$');
        Assert.Equal(3, segments.Length);
        Assert.Equal("600000", segments[0]);
        Assert.True(hasher.Verify("123456", hash));
        Assert.False(hasher.Verify("654321", hash));
    }

    [Fact]
    public void Hash_UsesRandomSalt_SoSamePasswordYieldsDifferentHashes()
    {
        var hasher = new PasswordHasher();

        Assert.NotEqual(hasher.Hash("123456"), hasher.Hash("123456"));
    }

    [Theory]
    [InlineData("$docgen$v1$600000$c2FsdA==$aGFzaA==", false)]
    [InlineData("pbkdf2:sha256:600000$c2FsdA==$aGFzaA==", true)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("aaaa:bbbb", true)]
    [InlineData("", true)]
    public void NeedsUpgrade_FlagsEveryNonCanonicalFormat(string storedHash, bool expected)
    {
        var hasher = new PasswordHasher();

        Assert.Equal(expected, hasher.NeedsUpgrade(storedHash));
    }

    [Fact]
    public void Verify_AcceptsLegacyHexPairFormat_WithFixedIterations()
    {
        // هاش بصيغة saltHex:hashHex القديمة (200k) يبقى صالحًا أثناء الفترة الانتقالية.
        byte[] salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        byte[] key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            "123456", salt, 200_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        var legacy = Convert.ToHexString(salt).ToLowerInvariant() + ":"
            + Convert.ToHexString(key).ToLowerInvariant();
        var hasher = new PasswordHasher();

        Assert.True(hasher.Verify("123456", legacy));
        Assert.False(hasher.Verify("wrong", legacy));
        Assert.True(hasher.NeedsUpgrade(legacy));
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
        Assert.True(await limiter.TryRecordFailureAsync("k"));
        Assert.True(await limiter.IsAllowedAsync("k"));
        Assert.True(await limiter.TryRecordFailureAsync("k"));
        Assert.True(await limiter.IsAllowedAsync("k"));
        Assert.True(await limiter.TryRecordFailureAsync("k"));
        Assert.False(await limiter.IsAllowedAsync("k"));
        Assert.False(await limiter.TryRecordFailureAsync("k"));
    }

    [Fact]
    public async Task Reset_ClearsFailures()
    {
        var limiter = Create(max: 1);
        Assert.True(await limiter.TryRecordFailureAsync("k"));
        Assert.False(await limiter.IsAllowedAsync("k"));
        await limiter.ResetAsync("k");
        Assert.True(await limiter.IsAllowedAsync("k"));
    }

    [Fact]
    public async Task Keys_AreIsolated()
    {
        var limiter = Create(max: 1);
        Assert.True(await limiter.TryRecordFailureAsync("a"));
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
        Assert.True(await limiter.TryRecordFailureAsync("fresh"));

        Assert.Equal(0, await _db.LoginAttempts.CountAsync(a => a.Key == "old"));
        Assert.Equal(1, await _db.LoginAttempts.CountAsync(a => a.Key == "fresh"));
    }

    /// <summary>
    /// يثبت القضاء على سباق TOCTOU: حتى مع 12 محاولة متزامنة فلا يتجاوز عدد الصفوف
    /// المدرجة الحد أبدًا — الفحص والتسجيل في جملة إدراج مشروط واحدة ذرّية على المزودين.
    /// </summary>
    [Fact]
    public async Task ConcurrentFailures_NeverExceedMax()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ratelimit_{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Mode=ReadWriteCreate;Pooling=False";
        try
        {
            using (var setup = new DocGeneratorDbContext(new DbContextOptionsBuilder<DocGeneratorDbContext>()
                .UseSqlite(connectionString).Options))
            {
                setup.Database.EnsureCreated();
            }

            var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
                .UseSqlite(connectionString).Options;

            var tasks = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
            {
                using var db = new DocGeneratorDbContext(options);
                var limiter = new DbLoginRateLimiter(db,
                    Microsoft.Extensions.Options.Options.Create(new RateLimitOptions
                    {
                        MaxLoginAttempts = 5,
                        WindowMinutes = 5,
                    }));
                return await limiter.TryRecordFailureAsync("concurrent-key");
            }));

            var results = await Task.WhenAll(tasks);
            Assert.Equal(5, results.Count(r => r));

            using var verify = new DocGeneratorDbContext(options);
            Assert.Equal(5, await verify.LoginAttempts.CountAsync(a => a.Key == "concurrent-key"));
        }
        finally
        {
            File.Delete(path);
        }
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
