using System.Security.Cryptography;
using System.Text.Json;
using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Common;

/// <summary>
/// إعادة نتيجة مخزنة لطلب مكرر بنفس المفتاح (RF-011): تلتقطها نقطة النهاية
/// فتردّ الجسم حرفيًا (`200`) بدل تنفيذ جديد — بلا تغيير تواقيع الخدمات.
/// </summary>
public sealed class IdempotentReplayException : Exception
{
    public string ResponseBody { get; }

    public IdempotentReplayException(string responseBody)
        : base("طلب مكرر — أُعيدت النتيجة المخزنة")
    {
        ResponseBody = responseBody;
    }
}

/// <summary>
/// حارس مفاتيح عدم التكرار (RF-011): تطبيع المفتاح + بصمة الحمولة + بدء/إتمام/تحرير.
/// خيارات التسلسل (`Web`) واحدة للبصمة واللقطة فيثبت النص المخزن حرفيًا.
/// تعذّر المخزن لغير التكرار (جدول غائب في نافذة النشر) → تنفيذ بلا إزالة تكرار
/// (`fail-open` موثق — التوافر أولًا، وترتيب النشر هجرة-أولًا يجعل النافذة معدومة).
/// </summary>
public static class IdempotencyGuard
{
    /// <summary>ترويسة نقل المفتاح — لا تلوّث عقود `DTO`.</summary>
    public const string HeaderName = "X-Idempotency-Key";

    /// <summary>نافذة المفتاح الافتراضية (72 ساعة — قرار `BQ-032` بلا اعتراض).</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(72);

    /// <summary>
    /// خيارات التسلسل مطابقة لمخرجات `MVC` حرفيًا: تسمية `camelCase` + إبقاء
    /// العربية دون هروب — فاللقطة المخزنة بايتًا هي جسم الاستجابة نفسه.
    /// (مُكتشَف باختبار: المسمّيات الجاهزة تخفي فروق الترميز؛ التثبيت الصريح وحده يطابق.
    /// جسما الدمج/النقل ASCII خالص فلا يكشفان الفرق — وجسم الإنشاء العربي يكشفه.)
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// تطبيع المفتاح: الفارغ = غائب (مسار قديم)؛ المخالف الشكل يُرفَض `400`.
    /// </summary>
    public static string? NormalizeKey(string? raw)
    {
        var key = raw?.Trim();
        if (string.IsNullOrEmpty(key))
            return null;
        if (key.Length > 100 || !key.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'))
            throw new ArgumentException("مفتاح عدم التكرار غير صالح — استخدم UUID");
        return key;
    }

    /// <summary>بصمة الحمولة (`SHA256` hex) — الحمولة المختلفة نية جديدة تُنفَّذ طازجة.</summary>
    public static string Fingerprint<T>(T request)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions)));

    /// <summary>تسلسل لقطة الاستجابة المخزنة (يُعاد حرفيًا عند التكرار).</summary>
    public static string Snapshot<T>(T response)
        => JsonSerializer.Serialize(response, JsonOptions);

    /// <summary>
    /// بدء المفتاح: يُعيد الرد المخزن (يُلقي `IdempotentReplayException`)، أو يلقي
    /// تعارض المعالجة، أو يعيد التذكرة للحجز، أو null للمسار القديم/المفتوح.
    /// </summary>
    public static async Task<IdempotencyTicket?> BeginAsync(
        IIdempotencyStore? store,
        string operation,
        int userId,
        string? rawKey,
        string fingerprint,
        CancellationToken ct = default)
    {
        // المفتاح التالف يُرفَض `400` قبل أي مساس بالمخزن (خطأ عميل لا حالة مخزن).
        var key = NormalizeKey(rawKey);
        if (store is null || key is null)
            return null;

        IdempotencyBegin begin;
        try
        {
            begin = await store.BeginAsync(key, operation, userId, fingerprint,
                DateTime.UtcNow.Add(DefaultWindow), ct);
        }
        catch (Exception)
        {
            // مخزن متعذر (جدول غائب في نافذة النشر): تنفيذ بلا إزالة تكرار.
            return null;
        }

        if (begin.ReplayBody is not null)
            throw new IdempotentReplayException(begin.ReplayBody);
        if (begin.Busy)
            throw new DocumentConflictException("الطلب قيد المعالجة — أعد المحاولة بعد لحظات");
        return begin.Ticket;
    }

    /// <summary>إتمام الحجز بلقطة الاستجابة — متسامح (لا يُفشل العملية الناجحة).</summary>
    public static async Task CompleteAsync(
        IIdempotencyStore? store,
        IdempotencyTicket? ticket,
        string responseBody,
        CancellationToken ct = default)
    {
        if (store is null || ticket is null)
            return;
        try
        {
            await store.CompleteAsync(ticket, responseBody, ct);
        }
        catch (Exception)
        {
            // اللقطة رفاهية الإعادة لا شرط النجاح — العملية تمّت أصلًا.
        }
    }

    /// <summary>تحرير الحجز عند فشل العملية (لتُعاد المحاولة طازجة).</summary>
    public static async Task ReleaseAsync(
        IIdempotencyStore? store,
        IdempotencyTicket? ticket,
        CancellationToken ct = default)
    {
        if (store is null || ticket is null)
            return;
        await store.ReleaseAsync(ticket, ct);
    }
}
