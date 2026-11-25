using DocGenerator.Api.Authorization;
using DocGenerator.Api.Http;
using DocGenerator.Api.Security;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Statistics;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
[EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
public class StatisticsController : ControllerBase
{
    private readonly IQueryHandler<GetDashboardStatsQuery, DashboardStatsDto> _dashboard;
    private readonly IQueryHandler<GetMonthlyStatsQuery, List<MonthlyStatDto>> _monthly;
    private readonly IQueryHandler<GetBranchesSummaryQuery, List<BranchSummaryDto>> _branchesSummary;
    private readonly IQueryHandler<GetUserActivityQuery, List<UserActivityDto>> _userActivity;
    private readonly IQueryHandler<GetRemindersQuery, List<ReminderDto>> _reminders;
    private readonly IQueryHandler<GetManagerStatsQuery, ManagerStatsDto> _managerStats;
    private readonly IQueryHandler<GetManagerLawyerStatsQuery, List<ManagerLawyerStatDto>> _managerLawyerStats;
    private readonly IQueryHandler<GetPersonalStatsQuery, ManagerStatsDto> _personalStats;
    private readonly IQueryHandler<GetAvailablePeriodsQuery, List<MonthlyStatDto>> _availablePeriods;
    private readonly IUserCapabilities _capabilities;

    public StatisticsController(
        IQueryHandler<GetDashboardStatsQuery, DashboardStatsDto> dashboard,
        IQueryHandler<GetMonthlyStatsQuery, List<MonthlyStatDto>> monthly,
        IQueryHandler<GetBranchesSummaryQuery, List<BranchSummaryDto>> branchesSummary,
        IQueryHandler<GetUserActivityQuery, List<UserActivityDto>> userActivity,
        IQueryHandler<GetRemindersQuery, List<ReminderDto>> reminders,
        IQueryHandler<GetManagerStatsQuery, ManagerStatsDto> managerStats,
        IQueryHandler<GetManagerLawyerStatsQuery, List<ManagerLawyerStatDto>> managerLawyerStats,
        IQueryHandler<GetPersonalStatsQuery, ManagerStatsDto> personalStats,
        IQueryHandler<GetAvailablePeriodsQuery, List<MonthlyStatDto>> availablePeriods,
        IUserCapabilities capabilities)
    {
        _dashboard = dashboard;
        _monthly = monthly;
        _branchesSummary = branchesSummary;
        _userActivity = userActivity;
        _reminders = reminders;
        _managerStats = managerStats;
        _managerLawyerStats = managerLawyerStats;
        _personalStats = personalStats;
        _availablePeriods = availablePeriods;
        _capabilities = capabilities;
    }

    private bool HasFullAccess => _capabilities.Has(User, UserCapability.HasFullAccess);
    private bool IsHeadOrSubHead => _capabilities.Has(User, UserCapability.IsHeadOrSubHead);
    private bool IsHead => _capabilities.Has(User, UserCapability.IsHead);
    private bool IsSubHead => _capabilities.Has(User, UserCapability.IsSubHead);
    private bool IsLawyer => _capabilities.Has(User, UserCapability.IsLawyer);

    /// <summary>
    /// فرع الدور المقيَّد بالفرع (رئيس قسم/محامٍ): رفض صريح (400) عند غيابه —
    /// يمنع تفسير `null` ضمنيًا كـ«كل الفروع» في مستودع الإحصاءات (محامٍ بلا فرع
    /// كان يرى مجاميع كل الفروع). الرسالة عامة عمدًا لتغطية الدورين معًا.
    /// </summary>
    private ActionResult? RequireOwnBranch(out int branchId)
    {
        branchId = 0;
        var own = User.GetBranchId();
        if (own is null)
            // قراءة إحصاءات (D11): 403 لا 400 — الرقم يرسل الرسالة نفسها فالواجهة لا تتغير.
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status403Forbidden,
                ApiErrorCodes.BranchRequired,
                "الحساب غير مرتبط بفرع");
        branchId = own.Value;
        return null;
    }

    /// <summary>
    /// نطاق مالك الإحصاءات (§12/قرار 27): القسم لدوائر القسم وبلا دائرة، والشعبة
    /// لدوائر شعبته — بلا تدهور (رمز بلا شعبة مرفوض). الإدارة بلا قيد.
    /// </summary>
    private ActionResult? RequireOwnerScope(out int branchId, out int? ownerSectionId, out bool fullAccess)
    {
        branchId = 0;
        ownerSectionId = null;
        fullAccess = HasFullAccess;
        if (fullAccess)
            return null;
        var error = RequireOwnBranch(out branchId);
        if (error is not null) return error;
        if (IsSubHead)
        {
            var section = User.GetSectionId();
            if (section is null)
                return Forbid();
            ownerSectionId = section;
        }
        else if (!IsHead)
        {
            return Forbid();
        }
        return null;
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = "manager,admin,head,subhead,lawyer")]
    public async Task<ActionResult<DashboardStatsDto>> Dashboard(CancellationToken ct)
    {
        // الأدوار المقيَّدة (رئيس/شعبة/محامٍ — المندوب مستبعد بقيد الدور أعلاه):
        // الرؤساء بنطاق المالك (§12/قرار 27)، والمحامي بفرعه كما كان.
        // `null` هنا تعني الكل ولا تُمنح إلا لوصول المدير/المشرف الكامل.
        if (HasFullAccess)
            return Ok(await _dashboard.HandleAsync(new GetDashboardStatsQuery(null), ct));
        if (IsHeadOrSubHead)
        {
            var scopeError = RequireOwnerScope(out var own, out var ownerSectionId, out _);
            if (scopeError is not null) return scopeError;
            return Ok(await _dashboard.HandleAsync(
                new GetDashboardStatsQuery(own, ownerSectionId, false), ct));
        }
        var branchError = RequireOwnBranch(out var branch);
        if (branchError is not null) return branchError;
        return Ok(await _dashboard.HandleAsync(new GetDashboardStatsQuery(branch), ct));
    }

    [HttpGet("monthly-stats")]
    [Authorize(Roles = "manager,admin,head,subhead,lawyer")]
    public async Task<ActionResult<List<MonthlyStatDto>>> Monthly(CancellationToken ct)
    {
        // كـ `/dashboard` أعلاه: الرؤساء بنطاق المالك، والمحامي بفرعه.
        if (HasFullAccess)
            return Ok(await _monthly.HandleAsync(new GetMonthlyStatsQuery(null), ct));
        if (IsHeadOrSubHead)
        {
            var scopeError = RequireOwnerScope(out var own, out var ownerSectionId, out _);
            if (scopeError is not null) return scopeError;
            return Ok(await _monthly.HandleAsync(
                new GetMonthlyStatsQuery(own, ownerSectionId, false), ct));
        }
        var branchError = RequireOwnBranch(out var branch);
        if (branchError is not null) return branchError;
        return Ok(await _monthly.HandleAsync(new GetMonthlyStatsQuery(branch), ct));
    }

    [HttpGet("reminders")]
    [Authorize(Roles = "lawyer")]
    public async Task<ActionResult<List<ReminderDto>>> Reminders(CancellationToken ct)
    {
        return Ok(await _reminders.HandleAsync(new GetRemindersQuery(User.GetUserId()), ct));
    }

    [HttpGet("branches/summary")]
    [Authorize(Roles = "manager,admin")]
    public async Task<ActionResult<List<BranchSummaryDto>>> BranchesSummary(CancellationToken ct)
        => Ok(await _branchesSummary.HandleAsync(new GetBranchesSummaryQuery(), ct));

    [HttpGet("users/activity")]
    [Authorize(Roles = "manager,admin")]
    public async Task<ActionResult<List<UserActivityDto>>> UserActivity(CancellationToken ct)
        => Ok(await _userActivity.HandleAsync(new GetUserActivityQuery(), ct));

    [HttpGet("stats/manager")]
    [Authorize(Roles = "manager,admin,head,subhead")]
    public async Task<ActionResult<ManagerStatsDto>> ManagerStats(
        [FromQuery] StatsPeriod period = StatsPeriod.Yearly,
        [FromQuery] int? branchId = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int? quarter = null,
        CancellationToken ct = default)
    {
        var invalid = ValidatePeriod(period, year, month, quarter);
        if (invalid is not null)
            return invalid;

        // نطاق المالك (§12/قرار 27): رئيس القسم لإحصاء قسمه (تضييق مقصود)،
        // ورئيس الشعبة لشعبته، والمدير/المشرف للفرع ككل — ولا اختيار فرع آخر للرؤساء.
        if (HasFullAccess)
            return Ok(await _managerStats.HandleAsync(
                new GetManagerStatsQuery(period, branchId, year, month, quarter), ct));
        var scopeError = RequireOwnerScope(out var own, out var ownerSectionId, out _);
        if (scopeError is not null) return scopeError;
        return Ok(await _managerStats.HandleAsync(
            new GetManagerStatsQuery(period, own, year, month, quarter, ownerSectionId, false), ct));
    }

    [HttpGet("stats/manager/lawyers")]
    [Authorize(Roles = "manager,admin,head,subhead")]
    public async Task<ActionResult<List<ManagerLawyerStatDto>>> ManagerLawyerStats(
        [FromQuery] StatsPeriod period = StatsPeriod.Yearly,
        [FromQuery] int branchId = 0,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int? quarter = null,
        CancellationToken ct = default)
    {
        var invalid = ValidatePeriod(period, year, month, quarter);
        if (invalid is not null)
            return invalid;

        // جدول المحامين بالنطاق نفسه (§12): العدّ ضمن نطاق المالك فقط.
        if (HasFullAccess)
        {
            if (branchId <= 0)
                return ApiProblem.Result(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.ValidationFailed,
                    "branchId مطلوب لجدول محامي الفرع",
                    field: "branchId");
            return Ok(await _managerLawyerStats.HandleAsync(
                new GetManagerLawyerStatsQuery(period, branchId, year, month, quarter), ct));
        }
        var scopeError = RequireOwnerScope(out var own, out var ownerSectionId, out _);
        if (scopeError is not null) return scopeError;
        return Ok(await _managerLawyerStats.HandleAsync(
            new GetManagerLawyerStatsQuery(period, own, year, month, quarter, ownerSectionId, false), ct));
    }

    [HttpGet("stats/me")]
    [Authorize(Roles = "lawyer")]
    public async Task<ActionResult<ManagerStatsDto>> PersonalStats(
        [FromQuery] StatsPeriod period = StatsPeriod.Yearly,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int? quarter = null,
        CancellationToken ct = default)
    {
        var invalid = ValidatePeriod(period, year, month, quarter);
        if (invalid is not null)
            return invalid;

        return Ok(await _personalStats.HandleAsync(
            new GetPersonalStatsQuery(period, User.GetUserId(), year, month, quarter), ct));
    }

    /// <summary>
    /// الأشهر المتاحة التي قُيّدت فيها ملفات، لنطاق المستخدم:
    /// مشرف/مدير: كل الفروع (أو فرع محدد)، رئيس قسم: قسمه، رئيس شعبة: شعبته،
    /// محامٍ: ملفاته هو. الدور المقيَّد بلا فرع (رئيس/محامٍ) يُرفض صراحة (400).
    /// </summary>
    [HttpGet("stats/periods")]
    [Authorize(Roles = "manager,admin,head,subhead,lawyer")]
    public async Task<ActionResult<List<MonthlyStatDto>>> AvailablePeriods(
        [FromQuery] int? branchId = null,
        CancellationToken ct = default)
    {
        int? effectiveBranch = HasFullAccess ? branchId : null;
        if (IsHeadOrSubHead)
        {
            var scopeError = RequireOwnerScope(out var own, out var ownerSectionId, out _);
            if (scopeError is not null) return scopeError;
            return Ok(await _availablePeriods.HandleAsync(
                new GetAvailablePeriodsQuery(own, null, ownerSectionId, false), ct));
        }
        else if (IsLawyer)
        {
            // الحصر بالملفات يبقى عبر userId أدناه؛ الحارس هنا يرفض الحالة
            // الشاذة (محامٍ بلا فرع) بدل تمريرها بصمت.
            var error = RequireOwnBranch(out _);
            if (error is not null) return error;
        }
        var userId = IsLawyer ? User.GetUserId() : (int?)null;

        return Ok(await _availablePeriods.HandleAsync(
            new GetAvailablePeriodsQuery(effectiveBranch, userId), ct));
    }

    private ActionResult? ValidatePeriod(StatsPeriod period, int? year, int? month, int? quarter)
    {
        if (year is < 1900 or > 2100)
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.ValidationFailed,
                "سنة غير صالحة",
                field: "year");
        if (period == StatsPeriod.Monthly && month is < 1 or > 12)
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.ValidationFailed,
                "شهر غير صالح",
                field: "month");
        if (period == StatsPeriod.Quarterly && quarter is < 1 or > 4)
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.ValidationFailed,
                "ربع غير صالح",
                field: "quarter");
        return null;
    }
}
