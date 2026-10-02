using DocGenerator.Api.Middleware;

namespace DocGenerator.Api.Tests;

/// <summary>
/// سياسة قصّ/تجريد السجلات (`SEC-012`): تسطيح السطور ضد التزوير، تجريد الأسرار
/// غير الملتبسة (بريد/اعتمادات/`Bearer`)، وقصّ للسقف — مع بقاء الأرقام والأسماء
/// (تشخيص المبالغ والرسائل العربية لا يُمسّ).
/// </summary>
public sealed class LogSanitizerTests
{
    [Fact]
    public void Flatten_ReplacesLineBreaksWithSpaces()
    {
        // كل محرف سطري ← مسافة (نفس سلوك المتحكم السابق حرفيًا — `\r\n` ← مسافتان).
        Assert.Equal("سطر أول سطر ثانٍ  سطر ثالث",
            LogSanitizer.Flatten("سطر أول\nسطر ثانٍ\r\nسطر ثالث"));
    }

    [Fact]
    public void Redact_MasksEmail_KeepsSurroundingText()
    {
        var redacted = LogSanitizer.Redact("فشل الإرسال إلى lawyer@example.com اليوم");

        Assert.DoesNotContain("lawyer@example.com", redacted);
        Assert.Contains("[بريد محجوب]", redacted);
        Assert.Contains("فشل الإرسال إلى", redacted);
    }

    [Theory]
    [InlineData("password=Secret123", "password")]
    [InlineData("token: abc-def-123", "token")]
    [InlineData("api_key=XYZ789", "api_key")]
    [InlineData("كلمة المرور: 123456", "كلمة المرور")]
    public void Redact_MasksCredentialPairs_KeepsKey(string input, string key)
    {
        var redacted = LogSanitizer.Redact($"تعذّر الدخول {input} للمستخدم");

        Assert.DoesNotContain("Secret123", redacted);
        Assert.DoesNotContain("abc-def-123", redacted);
        Assert.DoesNotContain("XYZ789", redacted);
        Assert.Contains($"{key}=[محجوب]", redacted);
    }

    [Theory]
    [InlineData("call with Bearer abc123 header", "Bearer [محجوب]")]
    [InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.payload", "[محجوب]")]
    public void Redact_MasksBearerToken(string input, string expectedMarker)
    {
        var redacted = LogSanitizer.Redact(input);

        Assert.DoesNotContain("abc123", redacted);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9", redacted);
        Assert.Contains(expectedMarker, redacted);
    }

    [Fact]
    public void Redact_PreservesAmountsAndNames()
    {
        // المبالغ والأرقام القضائية والأسماء تشخيصية — لا تجريد لها.
        const string text = "الملف 1234 بمبلغ 1500000 ليرة للمدعى أحمد العلي";
        Assert.Equal(text, LogSanitizer.Redact(text));
    }

    [Fact]
    public void Clip_TruncatesBeyondMax_PreservesWithinMax()
    {
        Assert.Equal(new string('x', 10), LogSanitizer.Clip(new string('x', 50), 10));
        Assert.Equal("قصير", LogSanitizer.Clip("قصير", 10));
    }

    [Fact]
    public void SanitizeForLog_NullStaysNull_EmptyStaysEmpty()
    {
        Assert.Null(LogSanitizer.SanitizeForLog(null, 100));
        Assert.Equal("", LogSanitizer.SanitizeForLog("", 100));
    }

    [Fact]
    public void SanitizeForLog_AppliesFlattenRedactClipInOrder()
    {
        var sanitized = LogSanitizer.SanitizeForLog("a@b.com\npassword=x" + new string('y', 100), 20);

        Assert.NotNull(sanitized);
        Assert.DoesNotContain('\n', sanitized);
        Assert.DoesNotContain("a@b.com", sanitized);
        Assert.DoesNotContain("password=x", sanitized);
        Assert.True(sanitized.Length <= 20);
    }

    [Fact]
    public void SanitizeForResponse_BoundsAndCleansReflectedInput()
    {
        var sanitized = LogSanitizer.SanitizeForResponse(
            "مدخل user@example.com خاطئ\nسطر ثانٍ" + new string('z', 2000));

        Assert.DoesNotContain('\n', sanitized);
        Assert.DoesNotContain("user@example.com", sanitized);
        Assert.True(sanitized.Length <= LogSanitizer.ResponseMessageLimit);
        Assert.StartsWith("مدخل [بريد محجوب] خاطئ", sanitized);
    }
}
