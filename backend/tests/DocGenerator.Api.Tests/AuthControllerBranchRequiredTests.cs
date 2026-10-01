using System.Net;
using System.Text.Json;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// ترجمة <c>LoginStatus.BranchRequired</c> في المتحكم: 400 بالرسالة العامة (تشمل
/// رئيس القسم والمحامي) بلا جلسة. تُختبر هنا بخدمة مصادقة مزيفة، لأن الصف الشاذ
/// غير قابل للتجسيد في القاعدة (قيد <c>CK_Users_BranchRequiredForBranchRoles</c>).
/// </summary>
public sealed class AuthControllerBranchRequiredTests
{
    private sealed class BranchRequiredAuthService : IAuthService
    {
        // `LoginResult` هنا مؤهلة صراحة: الاسم نفسه معرّف في نطاق الاختبارات
        // (`ApiFactory.LoginResult`) لغرض مختلف تمامًا.
        public Task<DocGenerator.Application.DTOs.LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
            => Task.FromResult(new DocGenerator.Application.DTOs.LoginResult(LoginStatus.BranchRequired, null));
        public Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task<UserDto?> GetUserAsync(int userId, CancellationToken ct = default)
            => Task.FromResult<UserDto?>(null);
    }

    private sealed class AllowAllRateLimiter : ILoginRateLimiter
    {
        public Task<bool> IsAllowedAsync(string key, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> TryRecordFailureAsync(string key, CancellationToken ct = default) => Task.FromResult(true);
        public Task ResetAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoopAuditLogger : IAuditLogger
    {
        public Task LogAsync(string? userName, string actionType, int? documentId = null,
            string? documentType = null, string? details = null, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task LogManyAsync(IReadOnlyList<AuditLogEntry> entries, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task LogDocumentChangeAsync(string? userName, string actionType, int documentId,
            string? documentType, string details, IReadOnlyList<DocumentFieldChange> changes,
            CancellationToken ct = default)
            => Task.CompletedTask;
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

    [Fact]
    public async Task Login_BranchRequired_ReturnsBadRequestWithGeneralMessage()
    {
        var controller = new AuthController(
            new BranchRequiredAuthService(),
            new AllowAllRateLimiter(),
            Options.Create(new RateLimitOptions()),
            new JwtOptions(),
            new TestEnvironment(),
            new NoopAuditLogger());
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await controller.Login(new LoginRequest("شاذ", "123456"), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, bad.StatusCode);
        // يُقرأ النص بعد فكّ التسلسل: المسرِّع الافتراضي يهرّب العربية (`\uXXXX`).
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(bad.Value));
        Assert.Contains("الحساب غير مرتبط بفرع", json.RootElement.GetProperty("message").GetString());
    }
}
