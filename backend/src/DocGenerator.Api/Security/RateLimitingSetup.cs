using System.Security.Claims;
using System.Threading.RateLimiting;
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

    /// <summary>نفس رسالة الواجهة لحالة 429 (<c>frontend/src/api/client.ts</c>) ليتطابق العرض.</summary>
    public const string RejectionMessage = "طلبات كثيرة في وقت قصير — انتظر قليلًا وحاول مجددًا";

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { message = RejectionMessage }, cancellationToken);
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
            PermitLimit = Math.Max(1, perMinute),
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
}
