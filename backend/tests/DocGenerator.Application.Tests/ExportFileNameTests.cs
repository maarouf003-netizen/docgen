using DocGenerator.Application.Common.Security;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P2-09 — تنقية أسماء ملفات التصدير قبل الترويسة. الهدف: ألا يعتمد الأمان على
/// قائمة المحارف غير الصالحة الخاصة بالمنصة (Linux يقبل <c>"</c> وCR/LF).
/// </summary>
[Trait("Category", "Unit")]
public class ExportFileNameTests
{
    [Fact]
    public void RemovesControlCharactersAndColons_SoHeadersCannotBeInjected()
    {
        var sanitized = ExportFileName.Sanitize("أحمد\r\nSet-Cookie: x=1", "مستند");

        Assert.DoesNotContain('\r', sanitized);
        Assert.DoesNotContain('\n', sanitized);
        Assert.DoesNotContain(':', sanitized);
        Assert.DoesNotContain('"', sanitized);
        Assert.Equal("أحمدSet-Cookie x=1", sanitized);
    }

    [Fact]
    public void RemovesPathSeparators_SoNameCannotAddressAnotherDirectory()
    {
        Assert.Equal("etcpasswd", ExportFileName.Sanitize("../../etc/passwd", "مستند"));
        Assert.Equal("windows", ExportFileName.Sanitize(@"..\..\windows", "مستند"));
    }

    [Fact]
    public void RemovesBidiOverrides_SoExtensionsCannotBeSpoofed()
    {
        var sanitized = ExportFileName.Sanitize("invoice\u202Egnp.exe", "مستند");

        Assert.DoesNotContain('\u202E', sanitized);
        Assert.Equal("invoicegnp.exe", sanitized);
    }

    [Fact]
    public void PreservesArabicAndCollapsesWhitespace()
    {
        Assert.Equal("محمد أحمد", ExportFileName.Sanitize("  محمد   أحمد  ", "مستند"));
    }

    [Fact]
    public void EmptyOrWhitespaceValue_ReturnsFallback()
    {
        Assert.Equal("مستند", ExportFileName.Sanitize(null, "مستند"));
        Assert.Equal("مستند", ExportFileName.Sanitize("   ", "مستند"));
        Assert.Equal("مستند", ExportFileName.Sanitize("\r\n\t", "مستند"));
    }

    [Fact]
    public void VeryLongValue_IsCappedAtMaxLength()
    {
        var sanitized = ExportFileName.Sanitize(new string('ا', ExportFileName.MaxLength + 50), "مستند");

        Assert.Equal(ExportFileName.MaxLength, sanitized.Length);
    }

    [Fact]
    public void Truncation_DoesNotLeaveAnUnpairedSurrogate()
    {
        // 119 محرفًا + إيموجي (زوج surrogate) + محرفان: القص عند 120 كان سيبتر الزوج.
        var value = new string('a', 119) + "😀" + "bb";

        var sanitized = ExportFileName.Sanitize(value, "مستند");

        Assert.Equal(119, sanitized.Length);
        Assert.False(char.IsHighSurrogate(sanitized[^1]));
    }

    [Fact]
    public void FallbackMustNotBeBlank()
    {
        Assert.Throws<ArgumentException>(() => ExportFileName.Sanitize("name", " "));
    }
}
