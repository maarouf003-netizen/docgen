using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// مدخلات بناء مفتاح منع التكرار (<c>DedupeKey</c>) — حقول دلالية لا نص جاهز.
/// </summary>
public sealed record HeadAlertDedupeScope
{
    /// <summary>النوع الدلالي للتنبيه — إلزامي في كل مفتاح نظامي.</summary>
    public required string Kind { get; init; }

    /// <summary>الجمهور — يحدّد نطاق المفتاح (<c>recipient</c> أو <c>branch/audience</c>).</summary>
    public required HeadAlertAudience Audience { get; init; }

    /// <summary>الفرع — إلزامي في كل تنبيه، ويُستخدم في مفاتيح الجمهور المتعدد والمعلقات.</summary>
    public required int BranchId { get; init; }

    /// <summary>المستخدم المستهدف للجمهور الأحادي.</summary>
    public int? RecipientUserId { get; init; }

    public int? DelegationId { get; init; }
    public int? AppealId { get; init; }
    public int? ReviewLetterId { get; init; }
    public int? DocumentId { get; init; }
    public int? PublicEntityId { get; init; }
    public int? ExecutionCircuitId { get; init; }

    /// <summary>نوع عملية الجهة (<c>rename</c>/<c>merge</c>/<c>unify</c>/<c>abolish</c>/<c>move</c>).</summary>
    public string? EntityOperation { get; init; }

    /// <summary>مفتاح المجموعة (بديل معرّف الجهة في أحداث نقل مجموعة القيود).</summary>
    public string? EntityGroupKey { get; init; }

    /// <summary>اسم الدائرة المطبّع — بديل ثابت بدل معرّف الدائرة في مفتاح المعلقات.</summary>
    public string? CircuitKey { get; init; }
}

/// <summary>
/// بناء مفتاح منع التكرار (<c>DedupeKey</c>) مركزيًا — «تيار الحدث» لا النوع وحده.
/// المستدعي لا يبني المفتاح نصًّا ولا يمرّره، فيستحيل مفتاح بلا نوع أو بلا نطاق.
///
/// القوالب المعتمدة (الخطة §6.4):
/// <list type="bullet">
/// <item>إنابة + مستلم: <c>delegation:{id}:recipient:{userId}:{kind}</c></item>
/// <item>إنابة + جمهور فرع: <c>delegation:{id}:branch:{branchId}:audience:{heads|lawyers}:{kind}</c></item>
/// <item>معلق دائرة: <c>circuit-pending:{branchId}:{circuitId|hash}:recipient:{lawyerId}:{kind}</c></item>
/// <item>نقل دائرة: <c>circuit:{id}:recipient:{userId}:{kind}</c></item>
/// <item>استئناف: <c>appeal:{id}:recipient:{userId}:{kind}</c></item>
/// <item>ملف ومنشئه: <c>document:{documentId}:recipient:{userId}:{kind}</c></item>
/// <item>مستلم صريح بلا ملف: <c>document-recipient:recipient:{userId}:{kind}</c></item>
/// <item>كتاب مطالعة: <c>review:{letterId}:recipient:{userId}:{kind}</c></item>
/// <item>جهة: <c>entity:{operation}:{entityId|groupKey}:…:{kind}</c></item>
/// </list>
/// </summary>
public static class HeadAlertDedupe
{
    /// <summary>الحد الأقصى لطول المفتاح — يُرفض أطول منه قبل الحفظ.</summary>
    public const int MaxKeyLength = 200;

    /// <summary>بادئة الهاش المستخدمة عند تجاوز الحد — واضحة ولا تُقتطع بصمت.</summary>
    private const string HashPrefix = "h:";

    /// <summary>
    /// يبني مفتاح منع التكرار من الحقول الدلالية. يرفض النوع الفارغ/المحجوز/غير المعروف،
    /// ويرفض الجمهور الأحادي بلا مستلم، ولا يبني مفتاحًا بجزء فارغ.
    /// </summary>
    public static string Build(HeadAlertDedupeScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var kind = NormalizeToken(scope.Kind);
        if (string.IsNullOrEmpty(kind))
            throw new ArgumentException("نوع التنبيه مطلوب لبناء مفتاح منع التكرار", nameof(scope));
        if (kind == HeadAlertKindCatalog.Legacy)
            throw new ArgumentException("النوع المحجوز (legacy) لا يقبل مفتاح منع تكرار", nameof(scope));
        if (!HeadAlertKindCatalog.IsValid(kind))
            throw new ArgumentException($"نوع التنبيه غير معروف: {kind}", nameof(scope));

        var freeText = scope.EntityGroupKey ?? scope.CircuitKey;
        var key = Compose(scope, kind, freeText);
        if (key.Length <= MaxKeyLength)
            return key;

        // تجاوز الحد: يُستبدل الجزء النصي الطويل بهاش ثابت (لا بتر صامت).
        if (!string.IsNullOrWhiteSpace(freeText))
        {
            var hashed = Compose(scope, kind, ShortHash(freeText));
            if (hashed.Length <= MaxKeyLength)
                return hashed;
        }

        // ملاذ أخير: يُبقى النوع ظاهرًا في المفتاح (قاعدة «نوع إلزامي في كل مفتاح»).
        return $"{kind}:{HashPrefix}{ShortHash(key)}";
    }

    /// <summary>
    /// يستبدل مقطع المستلم بالكامل في مفتاح قائم: <c>recipient:{predecessor}:</c> ←
    /// <c>recipient:{successor}:</c> (بما فيه الفاصل الختامي، فلا يطابق معرّفًا آخر).
    /// يعيد المفتاح نفسه إن لم يوجد المقطع أو كان فارغًا/محجوزًا.
    /// </summary>
    public static string? RekeyRecipient(string? oldKey, int predecessorId, int successorId)
    {
        if (string.IsNullOrWhiteSpace(oldKey))
            return oldKey;
        if (oldKey == HeadAlertKindCatalog.Legacy)
            return oldKey;

        var needle = $"recipient:{predecessorId.ToString(CultureInfo.InvariantCulture)}:";
        if (!oldKey.Contains(needle, StringComparison.Ordinal))
            return oldKey;

        var replacement = $"recipient:{successorId.ToString(CultureInfo.InvariantCulture)}:";
        var rekeyed = oldKey.Replace(needle, replacement, StringComparison.Ordinal);
        if (rekeyed.Length <= MaxKeyLength)
            return rekeyed;

        // تجاوز الحد بعد الاستبدال: يُعاد بناء مفتاح ثابت يحافظ على النوع.
        var kindPart = rekeyed[(rekeyed.LastIndexOf(':') + 1)..];
        return $"{kindPart}:{HashPrefix}{ShortHash(rekeyed)}";
    }

    /// <summary>يتحقق من طول المفتاح المخزَّن — يرفض الأطول من الحد قبل الحفظ.</summary>
    public static string ValidateDedupeKey(string? key)
    {
        if (key is null)
            return string.Empty;
        var trimmed = key.Trim();
        if (trimmed.Length > MaxKeyLength)
            throw new ArgumentException(
                $"مفتاح منع التكرار يتجاوز الحد الأقصى المسموح ({MaxKeyLength} حرفًا)", nameof(key));
        return trimmed;
    }

    private static string Compose(HeadAlertDedupeScope scope, string kind, string? freeText)
        => $"{EventSegment(scope, freeText)}:{ScopeSegment(scope)}:{kind}";

    /// <summary>مقطع النطاق: مستلم واحد أو جمهور فرع.</summary>
    private static string ScopeSegment(HeadAlertDedupeScope scope) => scope.Audience switch
    {
        HeadAlertAudience.BranchLawyers =>
            $"branch:{scope.BranchId.ToString(CultureInfo.InvariantCulture)}:audience:lawyers",
        HeadAlertAudience.BranchHeads =>
            $"branch:{scope.BranchId.ToString(CultureInfo.InvariantCulture)}:audience:heads",
        _ => $"recipient:{RequireRecipient(scope).ToString(CultureInfo.InvariantCulture)}",
    };

    /// <summary>مقطع الحدث — يحدّد «تيار الحدث» الذي يُقارَن به.</summary>
    private static string EventSegment(HeadAlertDedupeScope scope, string? freeText)
    {
        // جمهور «منشئ الملف» يستخدم قالب الملف دائمًا ولو كان الحدث إنابة/استئنافًا.
        if (scope.Audience == HeadAlertAudience.DocumentCreator)
        {
            return scope.DocumentId.HasValue
                ? $"document:{scope.DocumentId.Value.ToString(CultureInfo.InvariantCulture)}"
                : "document-recipient";
        }

        if (scope.Kind == HeadAlertKindCatalog.CircuitPendingDocuments)
        {
            var token = scope.ExecutionCircuitId.HasValue
                ? scope.ExecutionCircuitId.Value.ToString(CultureInfo.InvariantCulture)
                : NormalizeToken(freeText);
            if (string.IsNullOrEmpty(token))
                throw new ArgumentException(
                    "مفتاح تنبيهات المعلقات يتطلب دائرة التنفيذ أو اسمها المطبّع", nameof(scope));
            return $"circuit-pending:{scope.BranchId.ToString(CultureInfo.InvariantCulture)}:{token}";
        }

        if (scope.DelegationId.HasValue)
            return $"delegation:{scope.DelegationId.Value.ToString(CultureInfo.InvariantCulture)}";
        if (scope.AppealId.HasValue)
            return $"appeal:{scope.AppealId.Value.ToString(CultureInfo.InvariantCulture)}";
        if (scope.ReviewLetterId.HasValue)
            return $"review:{scope.ReviewLetterId.Value.ToString(CultureInfo.InvariantCulture)}";
        if (scope.ExecutionCircuitId.HasValue)
            return $"circuit:{scope.ExecutionCircuitId.Value.ToString(CultureInfo.InvariantCulture)}";
        if (scope.PublicEntityId.HasValue)
        {
            var operation = NormalizeToken(scope.EntityOperation);
            if (string.IsNullOrEmpty(operation))
                throw new ArgumentException("حدث الجهة يحتاج نوع عملية صريحًا", nameof(scope));
            var entityToken = string.IsNullOrWhiteSpace(freeText)
                ? scope.PublicEntityId.Value.ToString(CultureInfo.InvariantCulture)
                : NormalizeToken(freeText);
            return $"entity:{operation}:{entityToken}";
        }
        if (scope.DocumentId.HasValue)
            return $"document:{scope.DocumentId.Value.ToString(CultureInfo.InvariantCulture)}";
        return "document-recipient";
    }

    private static int RequireRecipient(HeadAlertDedupeScope scope)
        => scope.RecipientUserId
           ?? throw new ArgumentException("الجمهور الأحادي يحتاج مستلمًا صريحًا", nameof(scope));

    /// <summary>
    /// تطبيع النص: قصّ الأطراف + توحيد runs المسافات + صيغة التوافق (NFKC) + أحرف صغيرة.
    /// </summary>
    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();

        // توحيد runs المسافات (بما therein الفراغ غير القابل للكسر) يمنع تكرار المفتاح
        // بسبب فراغ واحد زائد — وهو تطبيعوعي لا بتر.
        var builder = new StringBuilder(normalized.Length);
        var pendingSpace = false;
        foreach (var rune in normalized.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }

    /// <summary>هاش ثابت قصير (SHA-256) ببادئة واضحة — لا يقتطع النص بصمت.</summary>
    private static string ShortHash(string? value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return HashPrefix + Convert.ToHexString(bytes, 0, 16).ToLowerInvariant();
    }
}
