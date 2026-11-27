using System.Security.Cryptography;
using System.Text;

namespace DocGenerator.Application.Common;

/// <summary>
/// نطاق مفتاح عدم التكرار = الطريقة + **المسار الفعلي** (بما فيه معرّف المورد).
/// استخدام قالب المسار وحده كان يجعل `/documents/1/status` و`/documents/2/status`
/// نطاقًا واحدًا، فتُعاد نتيجة الملف الأول للثاني بلا تنفيذ (W1/B5).
/// </summary>
public static class IdempotencyOperation
{
    /// <summary>حد عمود <c>IdempotencyKeys.Operation</c>.</summary>
    public const int MaxLength = 100;

    /// <summary>يبني النطاق المطبَّع، ويختصره ببصمة ثابتة عند تجاوز حد العمود.</summary>
    public static string Build(string method, string path)
    {
        var normalizedPath = (path ?? string.Empty).Trim().TrimStart('/');
        if (normalizedPath.Length > 1)
            normalizedPath = normalizedPath.TrimEnd('/');

        var raw = $"{method.ToUpperInvariant()} {normalizedPath.ToLowerInvariant()}";
        if (raw.Length <= MaxLength)
            return raw;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16].ToLowerInvariant();
        return $"{method.ToUpperInvariant()} #{hash}";
    }
}
