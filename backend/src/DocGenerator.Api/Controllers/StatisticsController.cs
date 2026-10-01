using DocGenerator.Api.Authorization;
using DocGenerator.Api.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Application.Common.Interfaces;
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
    private readonly IStatisticsService _stats;
    private readonly IRepository<Branch> _branches;

    public StatisticsController(IStatisticsService stats, IRepository<Branch> branches)
    {
        _stats = stats;
        _branches = branches;
    }

    private UserRole Role => User.GetRoleEnum();

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
            return BadRequest(new { message = "الحساب غير مرتبط بفرع" });
        branchId = own.Value;
        return null;
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = "manager,admin,head,lawyer")]
    public async Task<ActionResult<DashboardStatsDto>> Dashboard(CancellationToken ct)
    {
        // الأدوار المقيَّدة (رئيس/محامٍ — المندوب مستبعد بقيد الدور أعلاه) تُحصر
        // في فرعها؛ `null` هنا تعني الكل ولا تُمنح إلا لوصول المدير/المشرف الكامل.
        int? branchId = null;
        if (!RolePermissions.HasFullAccess(Role))
        {
            var error = RequireOwnBranch(out var own);
            if (error is not null) return error;
            branchId = own;
        }
        return Ok(await _stats.GetDashboardStatsAsync(branchId, ct));
    }

    [HttpGet("monthly-stats")]
    [Authorize(Roles = "manager,admin,head,lawyer")]
    public async Task<ActionResult<List<MonthlyStatDto>>> Monthly(CancellationToken ct)
    {
        // كـ `/dashboard` أعلاه: لا شهريات كل الفروع لدور مقيَّد بلا فرع.
        int? branchId = null;
        if (!RolePermissions.HasFullAccess(Role))
        {
            var error = RequireOwnBranch(out var own);
            if (error is not null) return error;
            branchId = own;
        }
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
            var error = RequireOwnBranch(out var own);
            if (error is not null) return error;
            branchId = own;
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
            var error = RequireOwnBranch(out branchId);
            if (error is not null) return error;
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
    /// الدور المقيَّد بلا فرع (رئيس/محامٍ) يُرفض صراحة (400) بدل التسريب الضمني —
    /// المحامي محصور بملفاته أصلًا لكن غياب الفرع حالة شاذة تُرفض كالرئيس.
    /// </summary>
    [HttpGet("stats/periods")]
    [Authorize(Roles = "manager,admin,head,lawyer")]
    public async Task<ActionResult<List<MonthlyStatDto>>> AvailablePeriods(
        [FromQuery] int? branchId = null,
        CancellationToken ct = default)
    {
        int? effectiveBranch = RolePermissions.HasFullAccess(Role) ? branchId : null;
        if (Role == UserRole.Head)
        {
            var error = RequireOwnBranch(out var own);
            if (error is not null) return error;
            effectiveBranch = own;
        }
        else if (Role == UserRole.Lawyer)
        {
            // الحصر بالملفات يبقى عبر userId أدناه؛ الحارس هنا يرفض الحالة
            // الشاذة (محامٍ بلا فرع) بدل تمريرها بصمت.
            var error = RequireOwnBranch(out _);
            if (error is not null) return error;
        }
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
