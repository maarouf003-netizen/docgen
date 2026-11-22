using DocGenerator.Application.Common;
using DocGenerator.Infrastructure.Security;

namespace DocGenerator.Application.Tests;

/// <summary>
/// مؤشر keyset الموقّع: جولة ترميز/فك، رفض التلف والتلاعب، وربط المؤشر بنوع
/// القائمة — التوقيع حارس سلامة، والنطاق/الفلاتر تُطبَّق خادميًا دائمًا.
/// </summary>
public class KeysetCursorCodecTests
{
    private static KeysetCursorCodec Codec() => new(new JwtOptions
    {
        Secret = "tests-keyset-secret-0123456789abcdef",
    });

    [Fact]
    public void EncodeThenDecode_RoundTripsValueAndId()
    {
        var codec = Codec();
        var value = new DateTime(2026, 8, 1, 10, 30, 15, DateTimeKind.Utc);

        var encoded = codec.Encode(DocumentListCursorKinds.CreatedAt, value, 42);
        var cursor = codec.DecodeOptional(encoded, DocumentListCursorKinds.CreatedAt);

        Assert.NotNull(cursor);
        Assert.Equal(DocumentListCursorKinds.CreatedAt, cursor!.Kind);
        Assert.Equal(value, cursor.SortValue);
        Assert.Equal(42, cursor.Id);
    }

    [Fact]
    public void EncodeThenDecode_SupportsNullSortValue()
    {
        var codec = Codec();

        var encoded = codec.Encode(DocumentListCursorKinds.StruckOffDate, null, 7);
        var cursor = codec.DecodeOptional(encoded, DocumentListCursorKinds.StruckOffDate);

        Assert.NotNull(cursor);
        Assert.Null(cursor!.SortValue);
        Assert.Equal(7, cursor.Id);
    }

    [Fact]
    public void DecodeOptional_Empty_ReturnsNull()
    {
        var codec = Codec();
        Assert.Null(codec.DecodeOptional(null, DocumentListCursorKinds.CreatedAt));
        Assert.Null(codec.DecodeOptional("   ", DocumentListCursorKinds.CreatedAt));
    }

    [Fact]
    public void Decode_RejectsTamperedSignature()
    {
        var codec = Codec();
        var encoded = codec.Encode(DocumentListCursorKinds.CreatedAt, DateTime.UtcNow, 1);
        var tampered = encoded[..^1] + (encoded[^1] == 'A' ? 'B' : 'A');

        Assert.Throws<ArgumentException>(() =>
            codec.DecodeOptional(tampered, DocumentListCursorKinds.CreatedAt));
    }

    [Fact]
    public void Decode_RejectsKindMismatch()
    {
        var codec = Codec();
        var encoded = codec.Encode(DocumentListCursorKinds.CreatedAt, DateTime.UtcNow, 1);

        Assert.Throws<ArgumentException>(() =>
            codec.DecodeOptional(encoded, DocumentListCursorKinds.DeletedAt));
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("a.b.c")]
    [InlineData("###.###")]
    public void Decode_RejectsGarbage(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Codec().DecodeOptional(value, DocumentListCursorKinds.CreatedAt));
    }

    [Fact]
    public void Constructor_RejectsWeakSecret()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new KeysetCursorCodec(new JwtOptions { Secret = "short" }));
    }
}
