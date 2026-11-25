using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Auth;
using DocGenerator.Api.Auth;
using DocGenerator.Api.Filters;
using DocGenerator.Api.Http;
using DocGenerator.Api.Middleware;
using DocGenerator.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IQueryHandler<GetUserQuery, UserDto?> _getUser;
    private readonly ILoginRateLimiter _rateLimiter;
    private readonly RateLimitOptions _rateOptions;
    private readonly JwtOptions _jwt;
    private readonly IWebHostEnvironment _env;
    private readonly IAuditLogger _audit;
    private readonly ICommandHandler<LoginCommand, LoginResult> _login;
    private readonly ICommandHandler<ChangePasswordCommand, bool> _changePassword;

    /// <summary>نفس عمر Cookie الجلسة بالضبط حتى يتزامن زوجا Cookie (المصادقة + CSRF) عند الطرح والانتهاء.</summary>
    private TimeSpan SessionMaxAge => TimeSpan.FromMinutes(Math.Max(1, _jwt.ExpiryMinutes));

    /// <summary>
    /// `Retry-After` لرفض محدّد الدخول المعتمد على القاعدة (RFC 6585): نافذة المراقبة
    /// كاملة بالثواني، مقيَّدة بقيمة موجبة حتى لا يخرج `0` من إعداد خاطئ.
    /// </summary>
    private string LoginRetryAfterSeconds =>
        (Math.Max(1, _rateOptions.WindowMinutes) * 60)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

    public AuthController(
        IQueryHandler<GetUserQuery, UserDto?> getUser,
        ILoginRateLimiter rateLimiter,
        IOptions<RateLimitOptions> rateOptions,
        JwtOptions jwt,
        IWebHostEnvironment env,
        IAuditLogger audit,
        ICommandHandler<LoginCommand, LoginResult> login,
        ICommandHandler<ChangePasswordCommand, bool> changePassword)
    {
        _getUser = getUser;
        _rateLimiter = rateLimiter;
        _rateOptions = rateOptions.Value;
        _jwt = jwt;
        _env = env;
        _audit = audit;
        _login = login;
        _changePassword = changePassword;
    }

    [HttpPost("login")]
    [SkipIdempotency]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.LoginIpPolicy)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        // بعد UseForwardedHeaders الموثوق (KnownProxy فقط)، يعكس RemoteIpAddress عنوان
        // العميل الحقيقي من X-Forwarded-For الصادر عن البروكسي الموثوق — لا من أي عميل مباشر.
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateKey = $"{ip}:{request.Username.Trim().ToLowerInvariant()}";

        if (!await _rateLimiter.IsAllowedAsync(rateKey, ct))
        {
            Response.Headers.RetryAfter = LoginRetryAfterSeconds;
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status429TooManyRequests,
                ApiErrorCodes.RateLimited,
                $"محاولات دخول كثيرة جداً، حاول بعد {Math.Max(1, _rateOptions.WindowMinutes)} دقائق");
        }

        var result = await _login.HandleAsync(new LoginCommand(request), ct);
        return result.Status switch
        {
            LoginStatus.LockedOut => ApiProblem.Result(
                HttpContext,
                StatusCodes.Status423Locked,
                ApiErrorCodes.LockedOut,
                "الحساب مقفل مؤقتاً بسبب كثرة محاولات الدخول الفاشلة، حاول لاحقاً"),
            LoginStatus.Success => await HandleSuccessAsync(rateKey, result.Response!, ct),
            LoginStatus.InvalidCredentials => await HandleFailureAsync(rateKey, ct),
            LoginStatus.BranchSelectionRequired => Ok(new
            {
                requiresBranchSelection = true,
                branches = result.Branches ?? new List<LoginBranchChoiceDto>(),
            }),
            LoginStatus.BranchRequired => ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.BranchRequired,
                // عامة عمدًا: الحالة تشمل رئيس القسم والمحامي بلا فرع —
                // التفصيل بحسب الدور مسجّل في التدقيق لا في رسالة المستخدم.
                "الحساب غير مرتبط بفرع — تواصل مع المشرف لتعيين فرعك"),
            _ => throw new ArgumentOutOfRangeException(nameof(result.Status), result.Status,
                "حالة دخول غير معروفة"),
        };
    }

    private async Task<IActionResult> HandleSuccessAsync(string rateKey, LoginResponse response, CancellationToken ct)
    {
        await _rateLimiter.ResetAsync(rateKey, ct);
        Response.Cookies.Append(AuthCookie.Name, response.Token, AuthCookie.BuildOptions(_env, _jwt));
        Response.Cookies.Append(CsrfMiddleware.CookieName, CsrfMiddleware.CreateToken(),
            CsrfMiddleware.CookieOptions(_env, SessionMaxAge));
        // لا يُكشف التوكن في جسم الاستجابة؛ جلسته الوحيدة الـ Cookie.
        return Ok(new { user = response.User });
    }

    private async Task<IActionResult> HandleFailureAsync(string rateKey, CancellationToken ct)
    {
        // التسجيل ذرّي: إن بلغ الحد خلال سباق بين الفحص المبدئي والتسجيل تُرفض المحاولة فورًا.
        var recorded = await _rateLimiter.TryRecordFailureAsync(rateKey, ct);
        if (!recorded)
        {
            Response.Headers.RetryAfter = LoginRetryAfterSeconds;
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status429TooManyRequests,
                ApiErrorCodes.RateLimited,
                $"محاولات دخول كثيرة جداً، حاول بعد {Math.Max(1, _rateOptions.WindowMinutes)} دقائق");
        }
        return ApiProblem.Result(
                    HttpContext,
                    StatusCodes.Status401Unauthorized,
                    ApiErrorCodes.Unauthorized,
                    "اسم المستخدم أو كلمة المرور غير صحيحة");
    }

    [HttpPost("logout")]
    [SkipIdempotency]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        // [AllowAnonymous] حتى يُحذف الـ Cookie حتى لو انتهى التوكن أو أُبطلت صلاحيته.
        // RF-017 (SEC-009): توثيق الخروج باسم الجلسة (المتخفي بلا هوية يُتجاهَل تدقيقه لا حذفه).
        var userName = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
        if (!string.IsNullOrWhiteSpace(userName))
            await _audit.LogAsync(userName, "logout", details: "تسجيل خروج", ct: ct);
        Response.Cookies.Delete(AuthCookie.Name, AuthCookie.DeleteOptions(_env));
        Response.Cookies.Delete(CsrfMiddleware.CookieName, CsrfMiddleware.CookieOptions(_env, SessionMaxAge));
        return Ok(new { message = "تم تسجيل الخروج" });
    }

    [HttpPost("change-password")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin,entitymanager")]
    [EnableRateLimiting(RateLimitingSetup.PasswordPolicy)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var ok = await _changePassword.HandleAsync(
            new ChangePasswordCommand(User.GetUserId(), request.OldPassword, request.NewPassword), ct);
        if (!ok)
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.ValidationFailed,
                "كلمة المرور الحالية غير صحيحة",
                field: "oldPassword");
        return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
    }

    [HttpGet("me")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin,entitymanager")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await _getUser.HandleAsync(new GetUserQuery(User.GetUserId()), ct);
        return user is null ? NotFound() : Ok(user);
    }
}
