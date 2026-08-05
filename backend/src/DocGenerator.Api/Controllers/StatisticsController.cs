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
    public async Task<ActionResult<List<ReminderDto>>> Reminders(CancellationToken ct)
    {
        var branchId = RolePermissions.HasFullAccess(Role) ? (int?)null : User.GetBranchId();
        var userId = Role == UserRole.Lawyer ? User.GetUserId() : (int?)null;
        return Ok(await _stats.GetRemindersAsync(branchId, userId, ct));
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
    [Authorize(Roles = "manager,admin")]
    public async Task<ActionResult<ManagerStatsDto>> ManagerStats(
        [FromQuery] StatsPeriod period = StatsPeriod.Yearly,
        [FromQuery] int? branchId = null,
        CancellationToken ct = default)
        => Ok(await _stats.GetManagerStatsAsync(period, branchId, ct));

    [HttpGet("stats/manager/lawyers")]
    [Authorize(Roles = "manager,admin")]
    public async Task<ActionResult<List<ManagerLawyerStatDto>>> ManagerLawyerStats(
        [FromQuery] StatsPeriod period = StatsPeriod.Yearly,
        [FromQuery] int branchId = 0,
        CancellationToken ct = default)
    {
        if (branchId <= 0)
            return BadRequest(new { message = "branchId مطلوب لجدول محامي الفرع" });
        return Ok(await _stats.GetManagerLawyerStatsAsync(period, branchId, ct));
    }
}
