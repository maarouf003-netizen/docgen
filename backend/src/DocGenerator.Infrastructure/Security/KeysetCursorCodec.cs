using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DocGenerator.Application.Common;

namespace DocGenerator.Infrastructure.Security;

/// <summary>
/// مؤشرات keyset موقّعة بـ`HMAC-SHA256` بمفتاح مشتق من `Jwt:Secret` بسياق منفصل
/// (`docgen.keyset.v1`) فلا يصلح مفتاح التوقيع لشيء آخر ولا يتسرّب منه شيء.
/// التوقيع حارس سلامة لا حدود صلاحية: كل استعلام يطبّق النطاق والفلاتر خادميًا.
/// </summary>
public sealed class KeysetCursorCodec : IKeysetCursorCodec
{
    private const string Version = "v1";
    private const string KeyContext = "docgen.keyset.v1";
    private const char FieldSeparator = '|';
    private const char PartSeparator = '.';
    private const string NullMarker = "-";

    private readonly byte[] _key;

    public KeysetCursorCodec(JwtOptions jwt)
    {
        // نفس شرط JwtBearer (256 بت على الأقل) — لا مؤشر بمفتاح ضعيف.
        if (string.IsNullOrWhiteSpace(jwt.Secret) || Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
            throw new InvalidOperationException(
                "Jwt:Secret مطلوب بطول 32 بايتًا على الأقل لتوقيع مؤشرات keyset.");

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(KeyContext + FieldSeparator + jwt.Secret));
    }

    public string Encode(string kind, DateTime? sortValue, int id)
    {
        var payload = string.Join(
            FieldSeparator,
            Version,
            kind,
            sortValue?.ToString("O", CultureInfo.InvariantCulture) ?? NullMarker,
            id.ToString(CultureInfo.InvariantCulture));

        var signature = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
        return Base64Url(Encoding.UTF8.GetBytes(payload)) + PartSeparator + Base64Url(signature);
    }

    public KeysetCursor? DecodeOptional(string? value, string kind)
        => string.IsNullOrWhiteSpace(value) ? null : Decode(value, kind);

    private KeysetCursor Decode(string value, string kind)
    {
        var parts = value.Split(PartSeparator);
        if (parts.Length != 2)
            throw InvalidCursor();

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            signature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            throw InvalidCursor();
        }

        var expected = HMACSHA256.HashData(_key, payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(expected, signature))
            throw InvalidCursor();

        var fields = Encoding.UTF8.GetString(payloadBytes).Split(FieldSeparator);
        if (fields.Length != 4 || fields[0] != Version || fields[1] != kind)
            throw InvalidCursor();

        DateTime? sortValue = null;
        if (fields[2] != NullMarker)
        {
            if (!DateTime.TryParseExact(
                    fields[2],
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
                throw InvalidCursor();
            sortValue = parsed;
        }

        if (!int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw InvalidCursor();

        return new KeysetCursor(kind, sortValue, id);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };
        return Convert.FromBase64String(padded);
    }

    private static ArgumentException InvalidCursor()
        => new("مؤشر الصفحة غير صالح — أعد تحميل القائمة");
}
