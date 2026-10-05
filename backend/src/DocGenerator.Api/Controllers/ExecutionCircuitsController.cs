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

    public ExecutionCircuitsController(
        IExecutionCircuitService circuits,
        Application.Common.Interfaces.IRepository<Domain.Entities.Branch> branches)
    {
        _circuits = circuits;
        _branches = branches;
    }

    private UserRole Role => User.GetRoleEnum();
    private string? ActorName => User.Identity?.Name;
    private ActionResult? RequireHeadBranch(out int branchId)
    {
        branchId = 0;
        if (!RolePermissions.CanManageExecutionCircuits(Role))
            return Forbid();
        var own = User.GetBranchId();
        if (own is null)
            // S6.b: غياب سياق التفويض (claim الفرع) فشل تفويض لا طلب مشوَّه.
            return Forbid();
        branchId = own.Value;
        return null;
    }

    [HttpGet("mine")]
    [Authorize(Roles = "head")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var error = RequireHeadBranch(out var branchId);
        if (error is not null) return error;
        try
        {
            return Ok(await _circuits.ListMineAsync(branchId, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "head")]
    public async Task<IActionResult> Create([FromBody] UpsertExecutionCircuitRequest request, CancellationToken ct)
    {
        var error = RequireHeadBranch(out var branchId);
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
    [Authorize(Roles = "head")]
    public async Task<IActionResult> Rename(int id, [FromBody] UpsertExecutionCircuitRequest request, CancellationToken ct)
    {
        var error = RequireHeadBranch(out var branchId);
        if (error is not null) return error;
        try
        {
            return Ok(await _circuits.RenameAsync(id, branchId, ActorName, request.Name ?? string.Empty, request.Version, ct, User.GetUserId()));
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
    [Authorize(Roles = "head")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetCircuitActiveRequest request, CancellationToken ct)
    {
        var error = RequireHeadBranch(out var branchId);
        if (error is not null) return error;
        try
        {
            return Ok(await _circuits.SetActiveAsync(id, branchId, ActorName, request.IsActive, request.Version, ct));
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
    [Authorize(Roles = "head")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var error = RequireHeadBranch(out var branchId);
        if (error is not null) return error;
        try
        {
            await _circuits.DeleteAsync(id, branchId, ActorName, ct);
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
    [Authorize(Roles = "lawyer,head")]
    public async Task<IActionResult> ForLawyer(CancellationToken ct)
    {
        var branchId = User.GetBranchId();
        if (branchId is null)
            return Forbid();
        return Ok(await _circuits.ListForLawyerAsync(branchId.Value, ct));
    }

    [HttpGet("for-delegation")]
    [Authorize(Roles = "lawyer,head")]
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
    [Authorize(Roles = "head")]
    public async Task<IActionResult> ReferFiles(int id, [FromBody] ReferCircuitFilesRequest request, CancellationToken ct)
    {
        var error = RequireHeadBranch(out var branchId);
        if (error is not null) return error;
        try
        {
            var result = await _circuits.ReferFilesAsync(id, branchId, User.GetUserId(), ActorName, request, ct,
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
    [Authorize(Roles = "manager,admin,head")]
    public async Task<IActionResult> Stats([FromQuery] int? branchId, CancellationToken ct)
    {
        if (RolePermissions.HasFullAccess(Role))
            return Ok(await _circuits.CircuitStatsAsync(branchId, ct));
        var error = RequireHeadBranch(out var own);
        if (error is not null) return error;
        return Ok(await _circuits.CircuitStatsAsync(own, ct));
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
