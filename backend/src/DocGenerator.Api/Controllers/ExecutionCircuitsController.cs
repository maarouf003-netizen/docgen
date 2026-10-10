using DocGenerator.Api.Authorization;
using DocGenerator.Api.Security;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/execution-circuits")]
[Authorize]
[EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
public class ExecutionCircuitsController : ControllerBase
{
    private readonly IExecutionCircuitService _circuits;
    private readonly Application.Common.Interfaces.IRepository<Domain.Entities.Branch> _branches;
    private readonly IExcelExportService _excel;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ExecutionCircuitsController(
        IExecutionCircuitService circuits,
        Application.Common.Interfaces.IRepository<Domain.Entities.Branch> branches,
        IExcelExportService excel,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _circuits = circuits;
        _branches = branches;
        _excel = excel;
        _clock = clock;
        _timeZone = timeZone;
    }

    private UserRole Role => User.GetRoleEnum();
    private string? ActorName => User.Identity?.Name;
    /// <summary>
    /// نطاق المالك المشتق خادميًا (قرار §2.21): الفرع من الرمز، والشعبة من
    /// `section_id` لرئيس الشعبة — بلا أي نطاق من العميل. رئيس شعبة برمز بلا
    /// شعبة مرفوض (أعد الدخول — قرار §2.16) بدل التدهور لنطاق القسم.
    /// </summary>
    private ActionResult? RequireOwnerScope(out int branchId, out int? ownerSectionId)
    {
        branchId = 0;
        ownerSectionId = null;
        if (!RolePermissions.CanManageExecutionCircuits(Role))
            return Forbid();
        var own = User.GetBranchId();
        if (own is null)
            // S6.b: غياب سياق التفويض (claim الفرع) فشل تفويض لا طلب مشوَّه.
            return Forbid();
        branchId = own.Value;
        if (Role == UserRole.SubHead)
        {
            var section = User.GetSectionId();
            if (section is null)
                return Forbid();
            ownerSectionId = section;
        }
        return null;
    }

    [HttpGet("mine")]
    [Authorize(Policy = "HeadOrSubHead")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var error = RequireOwnerScope(out var branchId, out var ownerSectionId);
        if (error is not null) return error;
        try
        {
            return Ok(await _circuits.ListMineAsync(branchId, ownerSectionId, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "HeadOrSubHead")]
    public async Task<IActionResult> Create([FromBody] UpsertExecutionCircuitRequest request, CancellationToken ct)
    {
        var error = RequireOwnerScope(out var branchId, out _);
        if (error is not null) return error;
        try
        {
            var created = await _circuits.CreateAsync(branchId, User.GetUserId(), ActorName, request.Name ?? string.Empty, ct);
            return CreatedAtAction(nameof(Mine), new { }, created);
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPut("{id:int}/rename")]
    [Authorize(Policy = "HeadOrSubHead")]
    public async Task<IActionResult> Rename(int id, [FromBody] UpsertExecutionCircuitRequest request, CancellationToken ct)
    {
        var error = RequireOwnerScope(out var branchId, out var ownerSectionId);
        if (error is not null) return error;
        try
        {
            return Ok(await _circuits.RenameAsync(id, branchId, ownerSectionId, ActorName, request.Name ?? string.Empty, request.Version, ct, User.GetUserId()));
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPut("{id:int}/active")]
    [Authorize(Policy = "HeadOrSubHead")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetCircuitActiveRequest request, CancellationToken ct)
    {
        var error = RequireOwnerScope(out var branchId, out var ownerSectionId);
        if (error is not null) return error;
        try
        {
            return Ok(await _circuits.SetActiveAsync(id, branchId, ownerSectionId, ActorName, request.IsActive, request.Version, ct));
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "HeadOrSubHead")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var error = RequireOwnerScope(out var branchId, out var ownerSectionId);
        if (error is not null) return error;
        try
        {
            await _circuits.DeleteAsync(id, branchId, ownerSectionId, ActorName, ct);
            return NoContent();
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpGet("for-lawyer")]
    [Authorize(Roles = "lawyer,head,subhead")]
    public async Task<IActionResult> ForLawyer(CancellationToken ct)
    {
        var branchId = User.GetBranchId();
        if (branchId is null)
            return Forbid();
        // المحامي يرى كل دوائر الفرع (قرار §2.11)؛ الرئيس دوائر نطاقه فقط.
        var limitToOwner = Role is UserRole.Head or UserRole.SubHead;
        var ownerSectionId = limitToOwner ? User.GetSectionId() : null;
        if (limitToOwner && Role == UserRole.SubHead && ownerSectionId is null)
            return Forbid();
        return Ok(await _circuits.ListForLawyerAsync(branchId.Value, ownerSectionId, limitToOwner, ct));
    }

    [HttpGet("for-delegation")]
    [Authorize(Roles = "lawyer,head,subhead")]
    public async Task<IActionResult> ForDelegation(CancellationToken ct)
    {
        var branchId = User.GetBranchId();
        if (branchId is null)
            return Forbid();
        var branch = await _branches.GetByIdAsync(branchId.Value, ct);
        var branchGov = branch?.Governorate;
        if (string.IsNullOrWhiteSpace(branchGov))
            return Ok(new List<ExecutionCircuitDto>());
        return Ok(await _circuits.ListForDelegationAsync(branchGov, ct));
    }

    [HttpPost("{id:int}/refer-files")]
    [Authorize(Policy = "HeadOrSubHead")]
    public async Task<IActionResult> ReferFiles(int id, [FromBody] ReferCircuitFilesRequest request, CancellationToken ct)
    {
        var error = RequireOwnerScope(out var branchId, out var ownerSectionId);
        if (error is not null) return error;
        try
        {
            var result = await _circuits.ReferFilesAsync(id, branchId, ownerSectionId, User.GetUserId(), ActorName, request, ct,
                IdempotencyKeyHeader());
            return Ok(new
            {
                result.ReferredCount,
                result.SkippedCount,
                result.RemainingCount,
                reminder = ExecutionCircuitService.EmptyingReminderBanner,
            });
        }
        catch (IdempotentReplayException r)
        {
            return Content(r.ResponseBody, "application/json");
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>
    /// نقل ملكية دائرة لمالك جديد داخل الفرع نفسه (قسم الفرع أو شعبة فيه) —
    /// مشرف/مدير (قرار §2.5 + §8.3). `TargetSectionId` فارغ = قسم الفرع.
    /// </summary>
    [HttpPost("{id:int}/transfer")]
    [Authorize(Roles = "manager,admin")]
    public async Task<IActionResult> Transfer(int id, [FromBody] TransferCircuitRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _circuits.TransferCircuitAsync(id, request?.TargetSectionId, User.GetUserId(), ActorName, ct, request?.Version);
            return Ok(result);
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    private string? IdempotencyKeyHeader()
    {
        if (Request.Headers.TryGetValue(IdempotencyGuard.HeaderName, out var values))
        {
            var key = values.ToString();
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
        return null;
    }

    [HttpGet("stats")]
    [Authorize(Roles = "manager,admin,head,subhead")]
    public async Task<IActionResult> Stats([FromQuery] int? branchId, CancellationToken ct)
    {
        if (RolePermissions.HasFullAccess(Role))
            return Ok(await _circuits.CircuitStatsAsync(branchId, null, true, ct));
        var error = RequireOwnerScope(out var own, out var ownerSectionId);
        if (error is not null) return error;
        return Ok(await _circuits.CircuitStatsAsync(own, ownerSectionId, false, ct));
    }

    /// <summary>
    /// تصدير جدول إحصاءات الدوائر (§12) — بالنطاق نفسه لمسار `stats`
    /// (القسم/الشعبة/الفرع ككل) مع عمود الشعبة.
    /// </summary>
    [HttpGet("stats/export")]
    [Authorize(Roles = "manager,admin,head,subhead")]
    [SingleExportPerUser]
    public async Task<IActionResult> ExportStats([FromQuery] int? branchId, CancellationToken ct)
    {
        List<CircuitStatsDto> rows;
        if (RolePermissions.HasFullAccess(Role))
        {
            rows = await _circuits.CircuitStatsAsync(branchId, null, true, ct);
        }
        else
        {
            var error = RequireOwnerScope(out var own, out var ownerSectionId);
            if (error is not null) return error;
            rows = await _circuits.CircuitStatsAsync(own, ownerSectionId, false, ct);
        }
        var stream = _excel.BuildCircuitStatsWorkbook(rows);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"إحصاءات الدوائر {ServerClock.TodayString(_clock, _timeZone, "yyyy-MM-dd")}.xlsx");
    }
}

[ApiController]
[Route("api/documents")]
[Authorize]
[EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
public class PendingRegistrationsController : ControllerBase
{
    private readonly IExecutionCircuitService _circuits;

    public PendingRegistrationsController(IExecutionCircuitService circuits)
    {
        _circuits = circuits;
    }

    private UserRole Role => User.GetRoleEnum();
    private string? ActorName => User.Identity?.Name;

    [HttpGet("my-pending-registrations")]
    [Authorize(Roles = "lawyer")]
    public async Task<IActionResult> MyPending([FromQuery] int? circuitId, CancellationToken ct)
    {
        return Ok(await _circuits.MyPendingRegistrationsAsync(User.GetUserId(), circuitId, ct));
    }

    [HttpPost("complete-registrations")]
    [Authorize(Roles = "lawyer")]
    public async Task<IActionResult> Complete([FromBody] CompleteRegistrationsRequest request, CancellationToken ct)
    {
        try
        {
            var count = await _circuits.CompleteRegistrationsAsync(User.GetUserId(), ActorName, request, ct);
            return Ok(new { completedCount = count });
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }
}
