using System.Net;
using System.Text.Json;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Auth;
using DocGenerator.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// ترجمة <c>LoginStatus.BranchRequired</c> في المتحكم: 400 بالرسالة العامة (تشمل
/// رئيس القسم والمحامي) بلا جلسة. تُختبر هنا بمعالج دخول مزيف، لأن الصف الشاذ
/// غير قابل للتجسيد في القاعدة (قيد <c>CK_Users_BranchRequiredForBranchRoles</c>).
/// </summary>
public sealed class AuthControllerBranchRequiredTests
{
    private sealed class BranchRequiredLoginHandler : ICommandHandler<LoginCommand, DocGenerator.Application.DTOs.LoginResult>
    {
        // النوع مؤهَّل صراحة: الاسم نفسه معرّف في نطاق الاختبارات
        // (`ApiFactory.LoginResult`) لغرض مختلف تمامًا.
        public Task<DocGenerator.Application.DTOs.LoginResult> HandleAsync(
            LoginCommand command, CancellationToken ct = default)
            => Task.FromResult(new DocGenerator.Application.DTOs.LoginResult(LoginStatus.BranchRequired, null));
    }

    private sealed class StubGetUserHandler : IQueryHandler<GetUserQuery, UserDto?>
    {
        public Task<UserDto?> HandleAsync(GetUserQuery query, CancellationToken ct = default)
            => Task.FromResult<UserDto?>(null);
    }

    private sealed class AllowAllRateLimiter : ILoginRateLimiter
    {
        public Task<bool> IsAllowedAsync(string key, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> TryRecordFailureAsync(string key, CancellationToken ct = default) => Task.FromResult(true);
        public Task ResetAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "DocGenerator.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [PostgresFact]
    public async Task Login_BranchRequired_ReturnsBadRequestWithGeneralMessage()
    {
        var controller = new AuthController(
            new StubGetUserHandler(),
            new AllowAllRateLimiter(),
            Options.Create(new RateLimitOptions()),
            new JwtOptions(),
            new TestEnvironment(),
            new BranchRequiredLoginHandler(),
            null!,
            null!);
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await controller.Login(new LoginRequest("شاذ", "123456"), CancellationToken.None);

        var bad = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, bad.StatusCode);
        // يُقرأ النص بعد فكّ التسلسل: المسرِّع الافتراضي يهرّب العربية (`\uXXXX`).
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(bad.Value));
        Assert.Contains("الحساب غير مرتبط بفرع", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Login_RateLimitKey_NormalizesInvisibleUsernameCharacters()
    {
        // W2/F1: مفتاح الخنق كان `Trim().ToLowerInvariant()` بينما البحث يطبّع محارف
        // التنسيق (U+200B…) — فإرسال الاسم نفسه بمحرف صفري يتجاوز دلو المستخدم.
        var limiter = new CapturingRateLimiter();
        var controller = new AuthController(
            new StubGetUserHandler(),
            limiter,
            Options.Create(new RateLimitOptions()),
            new JwtOptions(),
            new TestEnvironment(),
            null!,
            null!,
            null!);
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        await controller.Login(new LoginRequest("admin\u200B\u200F ", "x"), CancellationToken.None);

        Assert.Equal($"{System.Net.IPAddress.Loopback}:admin", limiter.LastKey);
    }

    private sealed class CapturingRateLimiter : ILoginRateLimiter
    {
        public string? LastKey { get; private set; }

        public Task<bool> IsAllowedAsync(string key, CancellationToken ct = default)
        {
            LastKey = key;
            return Task.FromResult(false);
        }

        public Task<bool> TryRecordFailureAsync(string key, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task ResetAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }
}
