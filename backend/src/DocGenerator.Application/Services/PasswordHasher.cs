using System.Security.Cryptography;
using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Services;

/// <summary>
/// PBKDF2 (Rfc2898DeriveBytes, SHA-256, 200k iterations). Format: saltHex:hashHex.
/// متوافق مع صيغة التخزين المستخدمة في التطبيق الأصلي login_manager.py.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 200_000;

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return Convert.ToHexString(salt).ToLowerInvariant() + ":" + Convert.ToHexString(key).ToLowerInvariant();
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return false;

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
            && TryParseHex(parts[0], out var salt)
            && TryParseHex(parts[1], out var expected))
        {
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        // صيغة قديمة: SHA-256 بلا ملح (توافق مع التطبيق الأصلي)
        if (parts.Length == 1 && parts[0].Length == 64 && TryParseHex(parts[0], out var oldHash))
        {
            byte[] computed = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(computed, oldHash);
        }

        return false;
    }

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
