using System.Net;
using System.Security.Claims;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حراس النقاط الإحصائية (`RequireOwnBranch`): الدور المقيَّد بلا فرع (رئيس/محامٍ)
/// يُرفض صراحة بدل تمرير `null` (كل الفروع) إلى المستودع. تُختبر هنا بهوية
/// مطالبات مصنوعة وخدمة مزيفة، لأن قيد القاعدة يجعل تجسيد الصف الشاذ عبر EF
/// مستحيلًا — وهذا مقصود: المتحكم يحرس قرار الرفض، والقاعدة تحرس التخزين.
/// </summary>
public sealed class StatisticsBranchGuardTests
{
    private sealed class FakeStatisticsService : IStatisticsService
    {
        public Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default)
            => Task.FromResult(new DashboardStatsDto(0, 0, 0, 0, 0, 0, 0, 0));
        public Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default)
            => Task.FromResult(new List<MonthlyStatDto>());
        public Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<UserActivityDto>> GetUserActivityAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId, int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId, int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ManagerStatsDto> GetPersonalStatsAsync(StatsPeriod period, int userId, int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<MonthlyStatDto>> GetAvailablePeriodsAsync(int? branchId, int? userId, CancellationToken ct = default)
            => Task.FromResult(new List<MonthlyStatDto>());
    }

    private static StatisticsController ControllerFor(string role, int? branchId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "7"),
            new(ClaimTypes.Name, "tester"),
            new(ClaimTypes.Role, role),
        };
        if (branchId.HasValue)
            claims.Add(new Claim("branch_id", branchId.Value.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var controller = new StatisticsController(new FakeStatisticsService(), null!);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
        return controller;
    }

    private static void AssertBadRequestWithBranchMessage<T>(ActionResult<T> result)
    {
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.BadRequest, bad.StatusCode);
        // يُقرأ النص بعد فكّ التسلسل: المسرِّع الافتراضي يهرّب العربية (`\uXXXX`).
        using var json = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(bad.Value));
        Assert.Contains("غير مرتبط بفرع", json.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("head")]
    [InlineData("lawyer")]
    public async Task Dashboard_BranchlessBranchRole_BadRequest(string role)
    {
        var result = await ControllerFor(role, branchId: null).Dashboard(CancellationToken.None);

        AssertBadRequestWithBranchMessage(result);
    }

    [Theory]
    [InlineData("head")]
    [InlineData("lawyer")]
    public async Task MonthlyStats_BranchlessBranchRole_BadRequest(string role)
    {
        var result = await ControllerFor(role, branchId: null).Monthly(CancellationToken.None);

        AssertBadRequestWithBranchMessage(result);
    }

    [Theory]
    [InlineData("head")]
    [InlineData("lawyer")]
    public async Task AvailablePeriods_BranchlessBranchRole_BadRequest(string role)
    {
        var result = await ControllerFor(role, branchId: null).AvailablePeriods(null, CancellationToken.None);

        AssertBadRequestWithBranchMessage(result);
    }

    [Fact]
    public async Task Dashboard_LawyerWithBranch_Succeeds()
    {
        // الضابط الإيجابي: المحامي الشرعي بفرع يمرّ طبيعيًا.
        var result = await ControllerFor("lawyer", branchId: 3).Dashboard(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Dashboard_FullAccessWithoutBranch_Succeeds()
    {
        // الضابط الحدودي: `null` بمعنى «الكل» محفوظة لوصول المدير/المشرف الكامل.
        var result = await ControllerFor("manager", branchId: null).Dashboard(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.OK, ok.StatusCode);
    }
}
