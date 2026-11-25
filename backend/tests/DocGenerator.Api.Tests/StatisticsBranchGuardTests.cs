using System.Net;
using System.Security.Claims;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Statistics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حراس النقاط الإحصائية (`RequireOwnBranch`): الدور المقيَّد بلا فرع (رئيس/محامٍ)
/// يُرفض صراحة بدل تمرير `null` (كل الفروع) إلى المستودع. تُختبر هنا بهوية
/// مطالبات مصنوعة ومعالجات استعلام مزيفة، لأن قيد القاعدة يجعل تجسيد الصف الشاذ عبر
/// EF مستحيلًا — وهذا مقصود: المتحكم يحرس قرار الرفض، والقاعدة تحرس التخزين.
/// </summary>
public sealed class StatisticsBranchGuardTests
{
    private sealed class StubQueryHandler<TQuery, TResult> : IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        private readonly TResult _result;

        public StubQueryHandler(TResult result) => _result = result;

        public Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default)
            => Task.FromResult(_result);
    }

    private sealed class ThrowingQueryHandler<TQuery, TResult> : IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        public Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default)
            => throw new NotImplementedException();
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
        var controller = new StatisticsController(
            new StubQueryHandler<GetDashboardStatsQuery, DashboardStatsDto>(
                new DashboardStatsDto(0, 0, 0, 0, 0, 0, 0, 0)),
            new StubQueryHandler<GetMonthlyStatsQuery, List<MonthlyStatDto>>(new List<MonthlyStatDto>()),
            new ThrowingQueryHandler<GetBranchesSummaryQuery, List<BranchSummaryDto>>(),
            new ThrowingQueryHandler<GetUserActivityQuery, List<UserActivityDto>>(),
            new ThrowingQueryHandler<GetRemindersQuery, List<ReminderDto>>(),
            new ThrowingQueryHandler<GetManagerStatsQuery, ManagerStatsDto>(),
            new ThrowingQueryHandler<GetManagerLawyerStatsQuery, List<ManagerLawyerStatDto>>(),
            new ThrowingQueryHandler<GetPersonalStatsQuery, ManagerStatsDto>(),
            new StubQueryHandler<GetAvailablePeriodsQuery, List<MonthlyStatDto>>(new List<MonthlyStatDto>()),
            new DocGenerator.Api.Authorization.UserCapabilities());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
        return controller;
    }

    private static void AssertForbiddenWithBranchMessage<T>(ActionResult<T> result)
    {
        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.Forbidden, forbidden.StatusCode);
        // يُقرأ النص بعد فكّ التسلسل: المسرِّع الافتراضي يهرّب العربية (`\uXXXX`).
        using var json = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(forbidden.Value));
        Assert.Equal("branch_required", json.RootElement.GetProperty("code").GetString());
        Assert.Contains("غير مرتبط بفرع", json.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("head")]
    [InlineData("lawyer")]
    public async Task Dashboard_BranchlessBranchRole_Forbidden(string role)
    {
        var result = await ControllerFor(role, branchId: null).Dashboard(CancellationToken.None);

        AssertForbiddenWithBranchMessage(result);
    }

    [Theory]
    [InlineData("head")]
    [InlineData("lawyer")]
    public async Task MonthlyStats_BranchlessBranchRole_Forbidden(string role)
    {
        var result = await ControllerFor(role, branchId: null).Monthly(CancellationToken.None);

        AssertForbiddenWithBranchMessage(result);
    }

    [Theory]
    [InlineData("head")]
    [InlineData("lawyer")]
    public async Task AvailablePeriods_BranchlessBranchRole_Forbidden(string role)
    {
        var result = await ControllerFor(role, branchId: null).AvailablePeriods(null, CancellationToken.None);

        AssertForbiddenWithBranchMessage(result);
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
