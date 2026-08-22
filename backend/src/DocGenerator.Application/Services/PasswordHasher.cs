using System.Security.Cryptography;
using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Services;

/// <summary>
/// PBKDF2 (Rfc2898DeriveBytes, SHA-256). الصيغة المعيارية الحالية ذاتية الوصف:
/// "$docgen$v1$<iterations>$<saltBase64>$<hashBase64>" — عدد التكرارات مضمّن في الهاش
/// فيجوز رفع عامل العمل مستقبلًا دون كسر الهاشات القائمة. يقبل Verify أثناء الفترة
/// الانتقالية الصيغ التاريخية الثلاث: werkzeug (pbkdf2:sha256:<iters>$salt$hash)،
/// وصيغة saltHex:hashHex السابقة (200k)، وSHA-256 المجرد بلا ملح (إرث Flask).
/// NeedsUpgrade تعلن أي هاش ليس بالصيغة المعيارية ليعيد مسار الدخول تجزئته شفافيًا.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const string V1Prefix = "$docgen$v1$";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int CurrentIterations = 600_000;
    private const int LegacyHexFormatIterations = 200_000;

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, CurrentIterations, HashAlgorithmName.SHA256, KeySize);
        return V1Prefix + CurrentIterations + "$"
            + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(key);
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return false;

        // الصيغة المعيارية v1: معاملاتها مضمنة داخل النص.
        if (storedHash.StartsWith(V1Prefix, StringComparison.Ordinal))
        {
            var segments = storedHash[V1Prefix.Length..].Split('$');
            if (segments.Length == 3
                && int.TryParse(segments[0], out var iterations)
                && iterations > 0)
            {
                try
                {
                    var salt = Convert.FromBase64String(segments[1]);
                    var expected = Convert.FromBase64String(segments[2]);
                    byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                        password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                    return CryptographicOperations.FixedTimeEquals(actual, expected);
                }
                catch (FormatException)
                {
                    return false;
                }
            }

            return false;
        }

        // صيغة werkzeug (Flask): pbkdf2:sha256:600000$salt$hash (base64)
        const string werkzeugPrefix = "pbkdf2:sha256:";
        if (storedHash.StartsWith(werkzeugPrefix, StringComparison.Ordinal))
        {
            var rest = storedHash[werkzeugPrefix.Length..].Split('$');
            if (rest.Length == 3
                && int.TryParse(rest[0], out var iterations)
                && iterations > 0)
            {
                try
                {
                    var wkSalt = Convert.FromBase64String(rest[1]);
                    var wkExpected = Convert.FromBase64String(rest[2]);
                    byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                        password, wkSalt, iterations, HashAlgorithmName.SHA256, wkExpected.Length);
                    return CryptographicOperations.FixedTimeEquals(actual, wkExpected);
                }
                catch (FormatException)
                {
                    return false;
                }
            }
        }

        var parts = storedHash.Split(':');
        if (parts.Length == 2
            && parts[0].Length == SaltSize * 2
            && parts[1].Length == KeySize * 2
            && TryParseHex(parts[0], out var saltLegacy)
            && TryParseHex(parts[1], out var expectedLegacy))
        {
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password, saltLegacy, LegacyHexFormatIterations, HashAlgorithmName.SHA256, KeySize);
            return CryptographicOperations.FixedTimeEquals(actual, expectedLegacy);
        }

        // صيغة قديمة: SHA-256 بلا ملح (توافق مع التطبيق الأصلي)
        if (parts.Length == 1 && parts[0].Length == 64 && TryParseHex(parts[0], out var oldHash))
        {
            byte[] computed = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(computed, oldHash);
        }

        return false;
    }

    public bool NeedsUpgrade(string storedHash)
        => string.IsNullOrEmpty(storedHash)
            || !storedHash.StartsWith(V1Prefix, StringComparison.Ordinal);

    private static bool TryParseHex(string hex, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch
        {
            bytes = Array.Empty<byte>();
            return false;
        }
    }
}
