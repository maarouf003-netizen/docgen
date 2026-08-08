using DocGenerator.Api.Authorization;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IHeadAlertService _alerts;

    public AlertsController(IHeadAlertService alerts)
    {
        _alerts = alerts;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool IsLawyer => Role == UserRole.Lawyer;
    private bool IsHead => Role == UserRole.Head;
    private bool CanCreateAlerts => RolePermissions.CanCreateAlerts(Role);

    /// <summary>
    /// قائمة التنبيهات: المحامي يرى تنبيهاته، ورئيس القسم يرى تنبيهات فرعه.
    /// المدير/المشرف خارج نطاق التنبيهات.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (IsLawyer)
            return Ok(await _alerts.ListForLawyerAsync(User.GetUserId(), ct));

        if (IsHead)
        {
            var branchId = User.GetBranchId();
            if (branchId is null)
                return Forbid();
            return Ok(await _alerts.ListForHeadAsync(branchId.Value, ct));
        }

        return Forbid();
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        // عدّاد غير المقروء خاص بالمحامي المستلم؛ رئيس القسم يقرأه من قائمة تنبيهاته.
        if (!IsLawyer)
            return Forbid();
        var count = await _alerts.CountUnreadAsync(User.GetUserId(), ct);
        return Ok(new { count });
    }

    /// <summary>إصدار تنبيه — رئيس القسم لفرعه فقط.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHeadAlertRequest request, CancellationToken ct)
    {
        if (!CanCreateAlerts)
            return Forbid();

        var branchId = User.GetBranchId();
        if (branchId is null)
            return BadRequest(new { message = "رئيس القسم دون فرع لا يمكنه إصدار تنبيهات" });

        try
        {
            var alert = await _alerts.CreateAsync(request, User.GetUserId(), branchId.Value, ActorName, ct);
            return CreatedAtAction(nameof(Get), new { id = alert.Id }, alert);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        if (IsLawyer)
        {
            var list = await _alerts.ListForLawyerAsync(User.GetUserId(), ct);
            var alert = list.FirstOrDefault(a => a.Id == id);
            return alert is null ? NotFound() : Ok(alert);
        }

        if (IsHead)
        {
            var branchId = User.GetBranchId();
            if (branchId is null)
                return Forbid();
            var list = await _alerts.ListForHeadAsync(branchId.Value, ct);
            var alert = list.FirstOrDefault(a => a.Id == id);
            return alert is null ? NotFound() : Ok(alert);
        }

        return Forbid();
    }

    /// <summary>تعليم التنبيه كمقروء — المحامي المستلم فقط.</summary>
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        if (!IsLawyer)
            return Forbid();
        var ok = await _alerts.MarkReadAsync(id, User.GetUserId(), ct);
        return ok ? NoContent() : NotFound();
    }
}
