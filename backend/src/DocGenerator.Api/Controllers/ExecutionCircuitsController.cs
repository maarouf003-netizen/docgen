using DocGenerator.Api.Authorization;
using DocGenerator.Api.Filters;
using DocGenerator.Api.Http;
using DocGenerator.Api.Security;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.ExecutionCircuits;
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
    private readonly IQueryHandler<ListMineExecutionCircuitsQuery, List<ExecutionCircuitDto>> _listMine;
    private readonly IQueryHandler<ListCircuitFilesQuery, List<CircuitFileDto>> _listFiles;
    private readonly IQueryHandler<ListExecutionCircuitsForLawyerQuery, List<ExecutionCircuitDto>> _listForLawyer;
    private readonly IQueryHandler<ListExecutionCircuitsForDelegationQuery, List<ExecutionCircuitDto>> _listForDelegation;
    private readonly IQueryHandler<GetCircuitStatsQuery, List<CircuitStatsDto>> _circuitStats;
    private readonly Application.Common.Interfaces.IRepository<Domain.Entities.Branch> _branches;
    private readonly IExcelExportService _excel;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IUserCapabilities _capabilities;
    private readonly ICommandHandler<CreateExecutionCircuitCommand, ExecutionCircuitDto> _createCircuit;
    private readonly ICommandHandler<SetExecutionCircuitActiveCommand, ExecutionCircuitDto> _setCircuitActive;
    private readonly ICommandHandler<RenameExecutionCircuitCommand, ExecutionCircuitDto> _renameCircuit;
    private readonly ICommandHandler<DeleteExecutionCircuitCommand, bool> _deleteCircuit;
    private readonly ICommandHandler<TransferExecutionCircuitCommand, ExecutionCircuitDto> _transferCircuit;
    private readonly ICommandHandler<ReferCircuitFilesCommand, ReferCircuitFilesResult> _referFiles;

    public ExecutionCircuitsController(
        IQueryHandler<ListMineExecutionCircuitsQuery, List<ExecutionCircuitDto>> listMine,
        IQueryHandler<ListCircuitFilesQuery, List<CircuitFileDto>> listFiles,
        IQueryHandler<ListExecutionCircuitsForLawyerQuery, List<ExecutionCircuitDto>> listForLawyer,
        IQueryHandler<ListExecutionCircuitsForDelegationQuery, List<ExecutionCircuitDto>> listForDelegation,
        IQueryHandler<GetCircuitStatsQuery, List<CircuitStatsDto>> circuitStats,
        Application.Common.Interfaces.IRepository<Domain.Entities.Branch> branches,
        IExcelExportService excel,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IUserCapabilities capabilities,
        ICommandHandler<CreateExecutionCircuitCommand, ExecutionCircuitDto> createCircuit,
        ICommandHandler<SetExecutionCircuitActiveCommand, ExecutionCircuitDto> setCircuitActive,
        ICommandHandler<RenameExecutionCircuitCommand, ExecutionCircuitDto> renameCircuit,
        ICommandHandler<DeleteExecutionCircuitCommand, bool> deleteCircuit,
        ICommandHandler<TransferExecutionCircuitCommand, ExecutionCircuitDto> transferCircuit,
        ICommandHandler<ReferCircuitFilesCommand, ReferCircuitFilesResult> referFiles)
    {
        _listMine = listMine;
        _listFiles = listFiles;
        _listForLawyer = listForLawyer;
        _listForDelegation = listForDelegation;
        _circuitStats = circuitStats;
        _branches = branches;
        _excel = excel;
        _clock = clock;
        _timeZone = timeZone;
        _capabilities = capabilities;
        _createCircuit = createCircuit;
        _setCircuitActive = setCircuitActive;
        _renameCircuit = renameCircuit;
        _deleteCircuit = deleteCircuit;
        _transferCircuit = transferCircuit;
        _referFiles = referFiles;
    }

    private bool IsSubHead => _capabilities.Has(User, UserCapability.IsSubHead);
    private bool IsHeadOrSubHead => _capabilities.Has(User, UserCapability.IsHeadOrSubHead);
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
        if (!_capabilities.Has(User, UserCapability.CanManageExecutionCircuits))
            return Forbid();
        var own = User.GetBranchId();
        if (own is null)
            // S6.b: غياب سياق التفويض (claim الفرع) فشل تفويض لا طلب مشوَّه.
            return Forbid();
        branchId = own.Value;
        if (IsSubHead)
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
        return Ok(await _listMine.HandleAsync(
            new ListMineExecutionCircuitsQuery(branchId, ownerSectionId), ct));
    }

    /// <summary>نافذة الإفراغ (حزمة A): كل ملفات الدائرة غير المحذوفة أيًا كانت حالتها — نطاق المالك إلزامي.</summary>
    [HttpGet("{id:int}/files")]
    [Authorize(Policy = "HeadOrSubHead")]
    public async Task<IActionResult> Files(int id, CancellationToken ct)
    {
        var error = RequireOwnerScope(out var branchId, out var ownerSectionId);
        if (error is not null) return error;
        return Ok(await _listFiles.HandleAsync(
            new ListCircuitFilesQuery(id, branchId, ownerSectionId), ct));
    }

    [HttpPost]
    [Authorize(Policy = "HeadOrSubHead")]
    public async Task<IActionResult> Create([FromBody] UpsertExecutionCircuitRequest request, CancellationToken ct)
    {
        var error = RequireOwnerScope(out var branchId, out _);
        if (error is not null) return error;
        try
        {
            var created = await _createCircuit.HandleAsync(
                new CreateExecutionCircuitCommand(
                    branchId, User.GetUserId(), ActorName, request.Name ?? string.Empty), ct);
            return CreatedAtAction(nameof(Mine), new { }, created);
        }
        catch (DocumentConflictException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status409Conflict,
                            ApiErrorCodes.Conflict,
                            ApiErrorMessages.From(e));
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
            return Ok(await _renameCircuit.HandleAsync(
                new RenameExecutionCircuitCommand(
                    id, branchId, ownerSectionId, ActorName, request.Name ?? string.Empty,
                    request.Version, User.GetUserId()), ct));
        }
        catch (DocumentConflictException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status409Conflict,
                            ApiErrorCodes.Conflict,
                            ApiErrorMessages.From(e));
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
            return Ok(await _setCircuitActive.HandleAsync(
                new SetExecutionCircuitActiveCommand(
                    id, branchId, ownerSectionId, ActorName, request.IsActive, request.Version), ct));
        }
        catch (DocumentConflictException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status409Conflict,
                            ApiErrorCodes.Conflict,
                            ApiErrorMessages.From(e));
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
            await _deleteCircuit.HandleAsync(
                new DeleteExecutionCircuitCommand(id, branchId, ownerSectionId, ActorName), ct);
            return NoContent();
        }
        catch (DocumentConflictException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status409Conflict,
                            ApiErrorCodes.Conflict,
                            ApiErrorMessages.From(e));
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
        var limitToOwner = IsHeadOrSubHead;
        var ownerSectionId = limitToOwner ? User.GetSectionId() : null;
        if (limitToOwner && IsSubHead && ownerSectionId is null)
            return Forbid();
        return Ok(await _listForLawyer.HandleAsync(
            new ListExecutionCircuitsForLawyerQuery(branchId.Value, ownerSectionId, limitToOwner), ct));
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
        return Ok(await _listForDelegation.HandleAsync(
            new ListExecutionCircuitsForDelegationQuery(branchGov), ct));
    }

    [HttpPost("{id:int}/refer-files")]
    [SkipIdempotency]
    [Authorize(Policy = "HeadOrSubHead")]
    public async Task<IActionResult> ReferFiles(int id, [FromBody] ReferCircuitFilesRequest request, CancellationToken ct)
    {
        var error = RequireOwnerScope(out var branchId, out var ownerSectionId);
        if (error is not null) return error;
        try
        {
            var result = await _referFiles.HandleAsync(
                new ReferCircuitFilesCommand(
                    id, branchId, ownerSectionId, User.GetUserId(), ActorName, request, IdempotencyKeyHeader()), ct);
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
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status409Conflict,
                            ApiErrorCodes.Conflict,
                            ApiErrorMessages.From(e));
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
            var result = await _transferCircuit.HandleAsync(
                new TransferExecutionCircuitCommand(
                    id, request?.TargetSectionId, User.GetUserId(), ActorName, request?.Version), ct);
            return Ok(result);
        }
        catch (DocumentConflictException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status409Conflict,
                            ApiErrorCodes.Conflict,
                            ApiErrorMessages.From(e));
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
        if (_capabilities.Has(User, UserCapability.HasFullAccess))
            return Ok(await _circuitStats.HandleAsync(new GetCircuitStatsQuery(branchId, null, true), ct));
        var error = RequireOwnerScope(out var own, out var ownerSectionId);
        if (error is not null) return error;
        return Ok(await _circuitStats.HandleAsync(new GetCircuitStatsQuery(own, ownerSectionId, false), ct));
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
        if (_capabilities.Has(User, UserCapability.HasFullAccess))
        {
            rows = await _circuitStats.HandleAsync(new GetCircuitStatsQuery(branchId, null, true), ct);
        }
        else
        {
            var error = RequireOwnerScope(out var own, out var ownerSectionId);
            if (error is not null) return error;
            rows = await _circuitStats.HandleAsync(new GetCircuitStatsQuery(own, ownerSectionId, false), ct);
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
    private readonly IQueryHandler<ListMyPendingRegistrationsQuery, List<PendingRegistrationDto>> _pendingRegistrations;
    private readonly ICommandHandler<CompletePendingRegistrationsCommand, int> _completeRegistrations;

    public PendingRegistrationsController(
        IQueryHandler<ListMyPendingRegistrationsQuery, List<PendingRegistrationDto>> pendingRegistrations,
        ICommandHandler<CompletePendingRegistrationsCommand, int> completeRegistrations)
    {
        _pendingRegistrations = pendingRegistrations;
        _completeRegistrations = completeRegistrations;
    }

    private string? ActorName => User.Identity?.Name;

    [HttpGet("my-pending-registrations")]
    [Authorize(Roles = "lawyer")]
    public async Task<IActionResult> MyPending([FromQuery] int? circuitId, CancellationToken ct)
    {
        return Ok(await _pendingRegistrations.HandleAsync(
            new ListMyPendingRegistrationsQuery(User.GetUserId(), circuitId), ct));
    }

    [HttpPost("complete-registrations")]
    [Authorize(Roles = "lawyer")]
    public async Task<IActionResult> Complete([FromBody] CompleteRegistrationsRequest request, CancellationToken ct)
    {
        try
        {
            var count = await _completeRegistrations.HandleAsync(
                new CompletePendingRegistrationsCommand(User.GetUserId(), ActorName, request), ct);
            return Ok(new { completedCount = count });
        }
        catch (DocumentConflictException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status409Conflict,
                            ApiErrorCodes.Conflict,
                            ApiErrorMessages.From(e));
        }
    }
}
