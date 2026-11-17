using System.Security.Claims;
using System.Threading.RateLimiting;
using DocGenerator.Api.Http;
using DocGenerator.Application.Common;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Security;

/// <summary>
/// حد المعدل العام المتوازن (S4): محدد شامل لكل الطلبات (لكل مستخدم مصادَق، ولكل عنوان
/// <c>IP</c> للمجهول) + سياسات مسماة للمسارات الحساسة/المكلفة تُربط عبر
/// <c>[EnableRateLimiting]</c>. القيم كلها من <c>RateLimiting</c> في الإعدادات (نافذة دقيقة).
/// ملاحظة التوسع: المحدد المدمج ذاكرة محلية لكل عقدة — كافٍ لعقدة واحدة، وعند أي توسع
/// أفقي يُنقل الحد العام إلى مخزن مشترك (على نمط جدول <c>LoginAttempts</c>) لا عدّاد محلي.
/// </summary>
public static class RateLimitingSetup
{
    public const string LoginIpPolicy = "login-ip";
    public const string ExpensivePolicy = "expensive";
    public const string PasswordPolicy = "password";
    public const string CspReportPolicy = "csp-report";

    /// <summary>نفس رسالة الواجهة لحالة 429 (<c>frontend/src/api/client.ts</c>) ليتطابق العرض.</summary>
    public const string RejectionMessage = "طلبات كثيرة في وقت قصير — انتظر قليلًا وحاول مجددًا";

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            // RFC 6585: كل 429 يحمل Retry-After (نافذة السياسات دقيقة واحدة).
            context.HttpContext.Response.Headers.RetryAfter = "60";
            await ApiProblem.WriteAsync(
                context.HttpContext,
                StatusCodes.Status429TooManyRequests,
                ApiErrorCodes.RateLimited,
                RejectionMessage,
                cancellationToken: cancellationToken);
        };

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var limits = LimitsOf(context);
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is not null)
                return Partition($"user:{userId}", limits.GeneralAuthPerMinute);
            return Partition($"ip:{IpOf(context)}", limits.GeneralAnonPerMinute);
        });

        options.AddPolicy(LoginIpPolicy, context =>
            Partition($"login:{IpOf(context)}", LimitsOf(context).LoginIpPerMinute));

        options.AddPolicy(ExpensivePolicy, context =>
            Partition($"exp:{SubjectOf(context)}", LimitsOf(context).ExpensivePerMinute));

        options.AddPolicy(PasswordPolicy, context =>
            Partition($"pwd:{SubjectOf(context)}", LimitsOf(context).PasswordPerMinute));

        // تقارير CSP مجهولة: مفتاح IP دائمًا (لا هوية)، ونافذة دقيقة.
        options.AddPolicy(CspReportPolicy, context =>
            Partition($"csp:{IpOf(context)}", LimitsOf(context).CspReportPerMinute));
    }

    private static RateLimitOptions LimitsOf(HttpContext context)
        => context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;

    private static string IpOf(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string SubjectOf(HttpContext context)
        => context.User.FindFirstValue(ClaimTypes.NameIdentifier) is string userId
            ? $"user:{userId}"
            : $"ip:{IpOf(context)}";

    private static RateLimitPartition<string> Partition(string key, int perMinute)
        => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            // القيم محققة عند الإقلاع (RateLimitOptions.Validate) — لا تسطيح صامت هنا.
            PermitLimit = perMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
}
