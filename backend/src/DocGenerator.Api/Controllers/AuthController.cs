using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Api.Auth;
using DocGenerator.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ILoginRateLimiter _rateLimiter;
    private readonly RateLimitOptions _rateOptions;
    private readonly JwtOptions _jwt;
    private readonly IWebHostEnvironment _env;

    /// <summary>نفس عمر Cookie الجلسة بالضبط حتى يتزامن زوجا Cookie (المصادقة + CSRF) عند الطرح والانتهاء.</summary>
    private TimeSpan SessionMaxAge => TimeSpan.FromMinutes(Math.Max(1, _jwt.ExpiryMinutes));

    public AuthController(
        IAuthService auth,
        ILoginRateLimiter rateLimiter,
        IOptions<RateLimitOptions> rateOptions,
        JwtOptions jwt,
        IWebHostEnvironment env)
    {
        _auth = auth;
        _rateLimiter = rateLimiter;
        _rateOptions = rateOptions.Value;
        _jwt = jwt;
        _env = env;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        // بعد UseForwardedHeaders الموثوق (KnownProxy فقط)، يعكس RemoteIpAddress عنوان
        // العميل الحقيقي من X-Forwarded-For الصادر عن البروكسي الموثوق — لا من أي عميل مباشر.
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateKey = $"{ip}:{request.Username.Trim().ToLowerInvariant()}";

        if (!await _rateLimiter.IsAllowedAsync(rateKey, ct))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = $"محاولات دخول كثيرة جداً، حاول بعد {Math.Max(1, _rateOptions.WindowMinutes)} دقائق" });

        var result = await _auth.LoginAsync(request, ct);
        return result.Status switch
        {
            LoginStatus.LockedOut => StatusCode(StatusCodes.Status423Locked,
                new { message = "الحساب مقفل مؤقتاً بسبب كثرة محاولات الدخول الفاشلة، حاول لاحقاً" }),
            LoginStatus.Success => await HandleSuccessAsync(rateKey, result.Response!, ct),
            LoginStatus.InvalidCredentials => await HandleFailureAsync(rateKey, ct),
            LoginStatus.BranchSelectionRequired => Ok(new
            {
                requiresBranchSelection = true,
                branches = result.Branches ?? new List<LoginBranchChoiceDto>(),
            }),
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
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = $"محاولات دخول كثيرة جداً، حاول بعد {Math.Max(1, _rateOptions.WindowMinutes)} دقائق" });
        return Unauthorized(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        // [AllowAnonymous] حتى يُحذف الـ Cookie حتى لو انتهى التوكن أو أُبطلت صلاحيته.
        Response.Cookies.Delete(AuthCookie.Name, AuthCookie.DeleteOptions(_env));
        Response.Cookies.Delete(CsrfMiddleware.CookieName, CsrfMiddleware.CookieOptions(_env, SessionMaxAge));
        return Ok(new { message = "تم تسجيل الخروج" });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        try
        {
            var ok = await _auth.ChangePasswordAsync(User.GetUserId(), request.OldPassword, request.NewPassword, ct);
            if (!ok)
                return BadRequest(new { message = "كلمة المرور الحالية غير صحيحة" });
            return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await _auth.GetUserAsync(User.GetUserId(), ct);
        return user is null ? NotFound() : Ok(user);
    }
}
