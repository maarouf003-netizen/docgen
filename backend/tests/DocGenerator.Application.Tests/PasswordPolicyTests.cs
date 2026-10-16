using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات سياسة كلمة المرور الموحدة (M-02 — قرار المالك 2026-10-16):
/// حد 10 أحرف + رفض الشائعة والمتجانسة والمساوية لاسم الدخول.
/// </summary>
public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789")] // 9 أحرف — دون الحد
    [InlineData("abcdefghi")] // 9 أحرف — دون الحد
    [InlineData("  123456  ")] // 10 ظاهريًا بحشو المسافات، 6 فعليًا — لا يخدع الفحص
    [InlineData("  abc  ")] // 7 ظاهريًا بحشو المسافات — دون الحد بعد القص
    public void Validate_BelowMinLength_Rejected(string? password)
    {
        var error = PasswordPolicy.Validate(password);
        Assert.NotNull(error);
        Assert.Contains("10 أحرف", error);
    }

    [Theory]
    [InlineData("password123")]
    [InlineData("1234567890")]
    [InlineData("qwerty12345")]
    [InlineData("admin123456")]
    public void Validate_CommonPasswords_Rejected(string password)
    {
        Assert.NotNull(PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData("aaaaaaaaaa")]
    [InlineData("1111111111")]
    public void Validate_UniformPassword_Rejected(string password)
    {
        Assert.NotNull(PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData("TestPass1234", "testpass1234")] // تطابق بلا حساسية حالة
    [InlineData("TestPass1234", "  TestPass1234  ")] // تطابق بعد القص
    public void Validate_EqualsUsername_Rejected(string password, string username)
    {
        var error = PasswordPolicy.Validate(password, username);
        Assert.NotNull(error);
        Assert.Contains("اسم الدخول", error);
    }

    [Theory]
    [InlineData("TestPass1234", null)]
    [InlineData("TestPass1234", "other-user")]
    [InlineData("TestPass5678", "lawyer1")]
    public void Validate_StrongPassword_Accepted(string password, string? username)
    {
        Assert.Null(PasswordPolicy.Validate(password, username));
    }

    [Fact]
    public void EnsureValid_WeakPassword_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => PasswordPolicy.EnsureValid("short"));
        Assert.Contains("10 أحرف", ex.Message);
    }

    [Fact]
    public void EnsureValid_StrongPassword_DoesNotThrow()
    {
        var ex = Record.Exception(() => PasswordPolicy.EnsureValid("TestPass1234", "someone-else"));
        Assert.Null(ex);
    }
}
