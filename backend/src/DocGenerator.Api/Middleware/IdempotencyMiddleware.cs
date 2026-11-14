using System.Security.Cryptography;
using System.Text;
using DocGenerator.Api.Filters;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using Microsoft.AspNetCore.Routing;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// إزالة تكرار عامة لكل عملية كتابة (P0-04): إذا أرسل العميل
/// <c>X-Idempotency-Key</c> (أو <c>Idempotency-Key</c>) على
/// <c>POST/PUT/PATCH/DELETE</c> مصادَق، يُحجز المفتاح قبل تنفيذ الإجراء.
/// التكرار المكتمل يُعيد الحالة والجسم الأصليين حرفيًا؛ والمتشابه أثناء
/// المعالجة يُرفض <c>409</c>؛ وفشل العملية يحرّر الحجز لتُعاد طازجة.
///
/// <para>الحجز العام خارج معاملة العملية عمدًا (كما في `IdempotencyStore`)،
/// ولا يغطي إجراءات تحمل <see cref="SkipIdempotencyAttribute"/> لأنها تملك
/// حجزًا خدميًا أضيق أو ليست نية كتابة (دخول/خروج/تليمترية).</para>
/// </summary>
public sealed class IdempotencyMiddleware
{
    /// <summary>أقصى جسم استجابة يُخزَّن للإعادة (1MiB) — الاستجابات الأكبر تُترك بلا تخزين.</summary>
    private const int MaxStoredBodyBytes = 1024 * 1024;

    private static readonly string[] AlternativeHeaderNames = ["Idempotency-Key"];

    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        if (!TryResolve(context, out var key, out var operation, out var userId))
        {
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();
        var fingerprint = await FingerprintAsync(context.Request);

        IdempotencyBegin begin;
        try
        {
            begin = await store.BeginAsync(
                key,
                operation,
                userId,
                fingerprint,
                DateTime.UtcNow.Add(IdempotencyGuard.DefaultWindow),
                context.RequestAborted);
        }
        catch (Exception ex)
        {
            // نفس سياسة IdempotencyGuard: تعذّر المخزن لا يُسقِط الكتابة (fail-open).
            _logger.LogWarning(ex, "Idempotency store unavailable; executing write without deduplication.");
            await _next(context);
            return;
        }

        if (begin.ReplayBody is not null)
        {
            await WriteReplayAsync(context, begin);
            return;
        }

        if (begin.Busy)
            throw new DocumentConflictException("الطلب قيد المعالجة — أعد المحاولة بعد لحظات");

        // عقد المخزن يضمن تذكرة عند عدم الانشغال/الإعادة؛ حارس دفاعي يمنع NRE.
        if (begin.Ticket is null)
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        var stored = false;
        try
        {
            await _next(context);

            var status = context.Response.StatusCode;
            if (status is >= 200 and < 300 && buffer.Length <= MaxStoredBodyBytes)
            {
                var body = Encoding.UTF8.GetString(buffer.ToArray());
                var location = context.Response.Headers.Location.ToString();
                try
                {
                    await store.CompleteAsync(
                        begin.Ticket!,
                        body,
                        status,
                        context.Response.ContentType,
                        string.IsNullOrEmpty(location) ? null : location,
                        CancellationToken.None);
                    stored = true;
                }
                catch (Exception ex)
                {
                    // فشل توثيق الإعادة لا يُسقِط كتابة نجحت (مثل IdempotencyGuard).
                    _logger.LogWarning(ex, "Failed to persist idempotent replay snapshot.");
                }
            }
        }
        finally
        {
            context.Response.Body = originalBody;
            if (buffer.Length > 0)
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody, CancellationToken.None);
            }

            if (!stored)
                await ReleaseQuietlyAsync(store, begin.Ticket);
        }
    }

    private static bool TryResolve(
        HttpContext context,
        out string key,
        out string operation,
        out int userId)
    {
        key = string.Empty;
        operation = string.Empty;
        userId = 0;

        if (!HttpMethods.IsPost(context.Request.Method)
            && !HttpMethods.IsPut(context.Request.Method)
            && !HttpMethods.IsPatch(context.Request.Method)
            && !HttpMethods.IsDelete(context.Request.Method))
        {
            return false;
        }

        if (context.User.Identity?.IsAuthenticated != true)
            return false;

        var endpoint = context.GetEndpoint();
        if (endpoint is null || endpoint.Metadata.GetMetadata<SkipIdempotencyAttribute>() is not null)
            return false;

        var raw = context.Request.Headers[IdempotencyGuard.HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var alternative in AlternativeHeaderNames)
            {
                raw = context.Request.Headers[alternative].ToString();
                if (!string.IsNullOrWhiteSpace(raw))
                    break;
            }
        }

        var normalized = IdempotencyGuard.NormalizeKey(raw);
        if (normalized is null)
            return false;

        var route = (endpoint as RouteEndpoint)?.RoutePattern.RawText
            ?? context.Request.Path.Value
            ?? string.Empty;
        key = normalized;
        operation = BuildOperation(context.Request.Method, route);
        userId = context.User.GetUserId();
        return true;
    }

    /// <summary>
    /// يبني نطاق المفتاح من الطريقة والقالب. القالب قد يتجاوز حد العمود (100)؛
    /// عندها يُختصر ببصمة ثابتة بدل قصّ قابل للتصادم.
    /// </summary>
    private static string BuildOperation(string method, string route)
    {
        var operation = $"{method.ToUpperInvariant()} {route}";
        if (operation.Length <= 100)
            return operation;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operation)))[..16];
        return $"{method.ToUpperInvariant()} {hash}";
    }

    private static async Task<string> FingerprintAsync(HttpRequest request)
    {
        using var body = new MemoryStream();
        await request.Body.CopyToAsync(body, request.HttpContext.RequestAborted);
        request.Body.Position = 0;
        return IdempotencyGuard.FingerprintRaw(body.ToArray());
    }

    private static async Task WriteReplayAsync(HttpContext context, IdempotencyBegin begin)
    {
        context.Response.StatusCode = begin.ReplayStatusCode;
        if (!string.IsNullOrEmpty(begin.ReplayContentType))
            context.Response.ContentType = begin.ReplayContentType;
        if (!string.IsNullOrEmpty(begin.ReplayLocation))
            context.Response.Headers.Location = begin.ReplayLocation;

        if (begin.ReplayStatusCode == StatusCodes.Status204NoContent)
            return;

        var bytes = Encoding.UTF8.GetBytes(begin.ReplayBody!);
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }

    private static async Task ReleaseQuietlyAsync(IIdempotencyStore store, IdempotencyTicket? ticket)
    {
        if (ticket is null)
            return;

        try
        {
            await store.ReleaseAsync(ticket, CancellationToken.None);
        }
        catch
        {
            // التحرير أفضل جهد: بقاء الحجز «قيد المعالجة» آمن من تنفيذ مكرر.
        }
    }
}
