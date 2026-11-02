using System.Globalization;
using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// مؤشر الترقيم <see cref="AlertCursor"/> (الخطة §9.1، الدليل §7 البند 4): الصيغة السلكية
/// <c>{ISO-8601-UTC}|{id}</c> تُبنى من حقلي الصف وتُحلَّل بصرامة، والوقت غير المحدد/المحلي
/// يُرفض (فالمقارنة في القاعدة على UTC — انظر <c>UtcDateTimeConverter</c>).
/// </summary>
public class AlertCursorTests
{
    private static readonly DateTime Stamp =
        new(2026, 11, 2, 9, 30, 15, 123, DateTimeKind.Utc);

    [Fact]
    public void Format_ThenTryParse_RoundTripsStampAndId()
    {
        var wire = AlertCursor.Format(Stamp, 42);

        Assert.True(AlertCursor.TryParse(wire, out var cursor));
        Assert.NotNull(cursor);
        Assert.Equal(Stamp, cursor!.LastContentChangedAt);
        Assert.Equal(DateTimeKind.Utc, cursor.LastContentChangedAt.Kind);
        Assert.Equal(42, cursor.Id);
    }

    [Fact]
    public void Format_KeepsSubSecondPrecision_SoKeysetBoundaryIsExact()
    {
        // كسور الثواني جزء من المفتاح: اقتطاعها يخلط صفوفًا متقاربة زمنيًا.
        var wire = AlertCursor.Format(Stamp, 7);

        Assert.Contains(".123", wire, StringComparison.Ordinal);
        Assert.True(AlertCursor.TryParse(wire, out var cursor));
        Assert.Equal(Stamp.Ticks, cursor!.LastContentChangedAt.Ticks);
    }

    [Theory]
    [InlineData("2026-11-02T09:30:15Z", 5)]
    [InlineData("2026-11-02T09:30:15.1234567Z", 5)]
    [InlineData("2026-11-02T09:30:15+00:00", 5)]
    [InlineData("2026-11-02T09:30:15.1234567+00:00", 5)]
    public void TryParse_AcceptsExplicitUtcForms(string stamp, int id)
    {
        Assert.True(AlertCursor.TryParse($"{stamp}|{id}", out var cursor));
        Assert.NotNull(cursor);
        Assert.Equal(DateTimeKind.Utc, cursor!.LastContentChangedAt.Kind);
        Assert.Equal(id, cursor.Id);
    }

    [Theory]
    [InlineData("2026-11-02T09:30:15+03:00|5")] // منطقة غير UTC ⇒ تُرفض ولا تُحوَّل صامتة.
    [InlineData("2026-11-02T09:30:15|5")] // بلا منطقة ⇒ Kind=Unspecified.
    [InlineData("2026-11-02 09:30:15|5")] // صيغة غير ISO.
    public void TryParse_RejectsNonUtcOrAmbiguousTimestamps(string wire)
    {
        Assert.False(AlertCursor.TryParse(wire, out var cursor));
        Assert.Null(cursor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("2026-11-02T09:30:15Z")] // بلا فاصل.
    [InlineData("2026-11-02T09:30:15Z|")] // معرّف فارغ.
    [InlineData("|5")] // طابع فارغ.
    [InlineData("2026-11-02T09:30:15Z|0")] // المعرّف يبدأ من 1.
    [InlineData("2026-11-02T09:30:15Z|-3")]
    [InlineData("2026-11-02T09:30:15Z|abc")]
    [InlineData("2026-11-02T09:30:15Z|5.5")]
    [InlineData("not-a-date|5")]
    public void TryParse_RejectsMalformedInput(string? wire)
    {
        Assert.False(AlertCursor.TryParse(wire, out var cursor));
        Assert.Null(cursor);
    }

    [Fact]
    public void TryParse_TrimsSurroundingWhitespace()
    {
        var wire = "  " + AlertCursor.Format(Stamp, 11) + Environment.NewLine;

        Assert.True(AlertCursor.TryParse(wire, out var cursor));
        Assert.Equal(11, cursor!.Id);
    }

    [Fact]
    public void Parse_ValidWire_ReturnsCursor()
    {
        var cursor = AlertCursor.Parse(AlertCursor.Format(Stamp, 3));

        Assert.Equal(Stamp, cursor.LastContentChangedAt);
        Assert.Equal(3, cursor.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseOptional_MissingCursor_ReturnsNull_SoOffsetPagingStaysSupported(string? wire)
    {
        Assert.Null(AlertCursor.ParseOptional(wire));
    }

    [Fact]
    public void ParseOptional_ValidWire_ReturnsCursor()
    {
        var cursor = AlertCursor.ParseOptional(" " + AlertCursor.Format(Stamp, 8) + " ");

        Assert.NotNull(cursor);
        Assert.Equal(Stamp, cursor!.LastContentChangedAt);
        Assert.Equal(8, cursor.Id);
    }

    [Fact]
    public void ParseOptional_InvalidWire_Throws_SoShapeIsRejectedNotIgnored()
    {
        var error = Assert.Throws<ArgumentException>(() => AlertCursor.ParseOptional("garbage"));

        Assert.Contains("cursor غير صالحة", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_InvalidWire_ThrowsArabicArgumentExceptionWithParamName()
    {
        var error = Assert.Throws<ArgumentException>(() => AlertCursor.Parse("bad|0", "cursor"));

        Assert.Equal("cursor", error.ParamName);
        Assert.Contains("cursor غير صالحة", error.Message, StringComparison.Ordinal);
        // الرسالة للعرض المباشر: لا نص إطار عمل إنجليزي.
        Assert.DoesNotContain("was not recognized", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_NonUtcStamp_SerializesAsUtcWithoutDistortingInstant()
    {
        // أثر وقائي فقط: الكتابة في الأعمدة UTC حصرًا؛ ولو مرّ وقت محلي هنا
        // فلا تُنتج صيغة محلية تُرفض عند التحليل.
        var local = DateTime.SpecifyKind(Stamp, DateTimeKind.Local);
        var wire = AlertCursor.Format(local, 1);

        Assert.EndsWith("Z", wire.Split(AlertCursor.WireSeparator)[0], StringComparison.Ordinal);
        Assert.True(AlertCursor.TryParse(wire, out var cursor));
        Assert.Equal(DateTimeKind.Utc, cursor!.LastContentChangedAt.Kind);
    }

    [Fact]
    public void Format_UsesInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SY");
            var wire = AlertCursor.Format(Stamp, 9);

            // لا أرقام هندية عربية ولا فاصل عشري عربي في العقد السلكي.
            Assert.True(
                wire.StartsWith("2026-11-02T09:30:15.1230000Z|9", StringComparison.Ordinal),
                wire);
            Assert.True(AlertCursor.TryParse(wire, out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
