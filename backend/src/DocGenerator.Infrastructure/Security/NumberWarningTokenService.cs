using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Infrastructure.Security;

/// <summary>
/// رمز تحذير موقّع HMAC-SHA256 (بلا حالة/بلا جدول): يحمل معرّف المستخدم والملف
/// وبصمة الطلب وصفوف التعارض ووقت الانتهاء. المفتاح من سرّ JWT إن وُجد، وإلا مفتاح
/// عشوائي لكل عملية (الرموز لا تعبر إعادة التشغيل — وهذا مقبول: عمرها دقائق).
/// </summary>
public sealed class NumberWarningTokenService : INumberWarningTokenService
{
    private static readonly byte[] ProcessFallbackKey = RandomNumberGenerator.GetBytes(32);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private const char FieldSeparator = '\u001F';

    private readonly byte[] _key;

    public NumberWarningTokenService(JwtOptions? jwt = null)
    {
        _key = string.IsNullOrWhiteSpace(jwt?.Secret)
            ? ProcessFallbackKey
            : SHA256.HashData(Encoding.UTF8.GetBytes(jwt!.Secret));
    }

    public string Issue(int userId, int documentId, string fingerprint, IEnumerable<int> conflictingRowIds)
    {
        var expires = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds();
        var payload = BuildPayload(userId, documentId, fingerprint, conflictingRowIds, expires);
        var signature = Sign(payload);
        return $"{Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}.{Base64UrlEncode(signature)}";
    }

    public bool Validate(
        string? token, int userId, int documentId, string fingerprint, IEnumerable<int> conflictingRowIds)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;
        var parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;
        try
        {
            var payloadBytes = Base64UrlDecode(parts[0]);
            var signature = Base64UrlDecode(parts[1]);
            var expected = Sign(Encoding.UTF8.GetString(payloadBytes));
            if (!CryptographicOperations.FixedTimeEquals(signature, expected))
                return false;

            var payload = Encoding.UTF8.GetString(payloadBytes);
            var fields = payload.Split(FieldSeparator);
            if (fields.Length != 6)
                return false;
            if (!int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tokenUser)
                || tokenUser != userId)
                return false;
            if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tokenDoc)
                || tokenDoc != documentId)
                return false;
            if (!string.Equals(fields[2], fingerprint, StringComparison.Ordinal))
                return false;
            if (!long.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expires)
                || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
                return false;
            return string.Equals(fields[4], RowKey(conflictingRowIds), StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private byte[] Sign(string payload)
        => HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));

    private static string BuildPayload(
        int userId, int documentId, string fingerprint, IEnumerable<int> rowIds, long expires)
        => string.Join(FieldSeparator, userId.ToString(CultureInfo.InvariantCulture),
            documentId.ToString(CultureInfo.InvariantCulture),
            fingerprint,
            expires.ToString(CultureInfo.InvariantCulture),
            RowKey(rowIds),
            "v1");

    private static string RowKey(IEnumerable<int> rowIds)
        => string.Join(',', rowIds.Distinct().OrderBy(id => id).Select(id => id.ToString(CultureInfo.InvariantCulture)));

    private static string Base64UrlEncode(byte[] value)
        => Base64Url.EncodeToString(value);

    private static byte[] Base64UrlDecode(string value)
        => Base64Url.DecodeFromChars(value);
}
