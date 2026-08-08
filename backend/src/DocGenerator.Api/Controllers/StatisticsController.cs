using DocGenerator.Api.Authorization;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _stats;
    private readonly IRepository<Branch> _branches;

    public StatisticsController(IStatisticsService stats, IRepository<Branch> branches)
    {
        _stats = stats;
        _branches = branches;
    }

    private UserRole Role => User.GetRoleEnum();

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> Dashboard(CancellationToken ct)
    {
        var branchId = RolePermissions.HasFullAccess(Role) ? (int?)null : User.GetBranchId();
        return Ok(await _stats.GetDashboardStatsAsync(branchId, ct));
    }

    [HttpGet("monthly-stats")]
    public async Task<ActionResult<List<MonthlyStatDto>>> Monthly(CancellationToken ct)
    {
        var branchId = RolePermissions.HasFullAccess(Role) ? (int?)null : User.GetBranchId();
        return Ok(await _stats.GetMonthlyStatsAsync(branchId, ct));
    }

    [HttpGet("reminders")]
    [Authorize(Roles = "lawyer")]
    public async Task<ActionResult<List<ReminderDto>>> Reminders(CancellationToken ct)
    {
        return Ok(await _stats.GetRemindersAsync(User.GetUserId(), ct));
    }

    [HttpGet("branches/summary")]
    [Authorize(Roles = "manager,admin")]
    public async Task<ActionResult<List<BranchSummaryDto>>> BranchesSummary(CancellationToken ct)
        => Ok(await _stats.GetBranchesSummaryAsync(ct));

    [HttpGet("users/activity")]
    [Authorize(Roles = "manager,admin")]
    public async Task<ActionResult<List<UserActivityDto>>> UserActivity(CancellationToken ct)
        => Ok(await _stats.GetUserActivityAsync(ct));

    [HttpGet("stats/manager")]
    [Authorize(Roles = "manager,admin,head")]
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

        // رئيس القسم يُحتسب على فرعه فقط، ولا يحق له اختيار فرع آخر.
        if (Role == UserRole.Head)
        {
            var ownBranch = User.GetBranchId();
            if (ownBranch is null)
                return BadRequest(new { message = "رئيس القسم بلا فرع محدد" });
            branchId = ownBranch;
        }

        return Ok(await _stats.GetManagerStatsAsync(period, branchId, year, month, quarter, ct));
    }

    [HttpGet("stats/manager/lawyers")]
    [Authorize(Roles = "manager,admin,head")]
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

        // رئيس القسم يُحصر جدول المحامين في فرعه تلقائيًا.
        if (Role == UserRole.Head)
        {
            var ownBranch = User.GetBranchId();
            if (ownBranch is null)
                return BadRequest(new { message = "رئيس القسم بلا فرع محدد" });
            branchId = ownBranch.Value;
        }

        if (branchId <= 0)
            return BadRequest(new { message = "branchId مطلوب لجدول محامي الفرع" });
        return Ok(await _stats.GetManagerLawyerStatsAsync(period, branchId, year, month, quarter, ct));
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

        return Ok(await _stats.GetPersonalStatsAsync(period, User.GetUserId(), year, month, quarter, ct));
    }

    /// <summary>
    /// الأشهر المتاحة التي قُيّدت فيها ملفات، لنطاق المستخدم:
    /// مشرف/مدير: كل الفروع (أو فرع محدد)، رئيس قسم: فرعه، محامٍ: ملفاته هو.
    /// </summary>
    [HttpGet("stats/periods")]
    public async Task<ActionResult<List<MonthlyStatDto>>> AvailablePeriods(
        [FromQuery] int? branchId = null,
        CancellationToken ct = default)
    {
        int? effectiveBranch = RolePermissions.HasFullAccess(Role) ? branchId : null;
        if (Role == UserRole.Head)
            effectiveBranch = User.GetBranchId();
        var userId = Role == UserRole.Lawyer ? User.GetUserId() : (int?)null;

        return Ok(await _stats.GetAvailablePeriodsAsync(effectiveBranch, userId, ct));
    }

    private ActionResult? ValidatePeriod(StatsPeriod period, int? year, int? month, int? quarter)
    {
        if (year is < 1900 or > 2100)
            return BadRequest(new { message = "سنة غير صالحة" });
        if (period == StatsPeriod.Monthly && month is < 1 or > 12)
            return BadRequest(new { message = "شهر غير صالح" });
        if (period == StatsPeriod.Quarterly && quarter is < 1 or > 4)
            return BadRequest(new { message = "ربع غير صالح" });
        return null;
    }
}
