using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// قاعدة عامة: سنة الملف لا تكون مستقبلية في أي مدخل (إنشاء/تعديل/تسجيل أصولًا/
/// إعادة قيد/سنة إعادة) — الحد الأقصى السنة الجارية.
/// </summary>
public class FileYearGuardTests
{
    [Fact]
    public void FutureYear_Throws_IncludingArabicDigits()
    {
        var ex = Assert.Throws<ArgumentException>(() => DocumentValidator.ValidateFileYearNotFuture("2027", 2026));
        Assert.Contains("لم تأتِ بعد", ex.Message);
        Assert.Throws<ArgumentException>(() => DocumentValidator.ValidateFileYearNotFuture("٢٠٢٧", 2026));
        Assert.Throws<ArgumentException>(() => DocumentValidator.ValidateFileYearNotFuture(" 2028 ", 2026));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026")]
    [InlineData("2025")]
    [InlineData("٢٠٢٦")]
    [InlineData("1999")]
    [InlineData("not-a-year")]
    public void CurrentPastEmptyOrNonNumeric_PassThrough(string? fileYear)
    {
        DocumentValidator.ValidateFileYearNotFuture(fileYear, 2026);
    }
}
