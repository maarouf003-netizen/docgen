using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
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

    public AuthController(IAuthService auth, ILoginRateLimiter rateLimiter, IOptions<RateLimitOptions> rateOptions)
    {
        _auth = auth;
        _rateLimiter = rateLimiter;
        _rateOptions = rateOptions.Value;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
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
        return Ok(response);
    }

    private async Task<IActionResult> HandleFailureAsync(string rateKey, CancellationToken ct)
    {
        await _rateLimiter.RecordFailureAsync(rateKey, ct);
        return Unauthorized(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
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
