using DocGenerator.Api.Authorization;
using DocGenerator.Api.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/personal-reminders")]
[Authorize(Roles = "lawyer")]
public class PersonalRemindersController : ControllerBase
{
    private readonly IPersonalReminderService _reminders;

    public PersonalRemindersController(IPersonalReminderService reminders)
    {
        _reminders = reminders;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool CanManage => RolePermissions.CanManagePersonalReminders(Role);

    /// <summary>تذكيرات المحامي الشخصية — المالك فقط (`includeArchived` للأرشيف).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        if (!CanManage)
            return Forbid();
        return Ok(await _reminders.ListAsync(User.GetUserId(), includeArchived, ct));
    }

    /// <summary>إنشاء تذكير شخصي — المالك الحالي حصرًا.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    public async Task<IActionResult> Create([FromBody] CreatePersonalReminderRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var reminder = await _reminders.CreateAsync(request, User.GetUserId(), ActorName, ct);
            return CreatedAtAction(nameof(Get), new { id = reminder.Id }, reminder);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        var reminder = await _reminders.GetByIdAsync(id, User.GetUserId(), ct);
        return reminder is null ? NotFound() : Ok(reminder);
    }

    /// <summary>تعديل تذكير شخصي (بما فيها الأرشفة الصريحة) — المالك فقط.</summary>
    [HttpPut("{id:int}")]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePersonalReminderRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var reminder = await _reminders.UpdateAsync(id, request, User.GetUserId(), ActorName, ct);
            return reminder is null ? NotFound() : Ok(reminder);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>تعليم تكرارٍ ما منجزًا أو إعادته — المالك فقط.</summary>
    [HttpPatch("{id:int}/occurrences")]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    public async Task<IActionResult> SetOccurrence(int id, [FromBody] SetOccurrenceRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var reminder = await _reminders.SetOccurrenceAsync(id, request.OccurrenceDate, request.Done, User.GetUserId(), ct);
            return reminder is null ? NotFound() : Ok(reminder);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>حذف التذكير الشخصي مع كل تكراراته (فعل خطر — الواجهة تطلب تأكيدًا) — المالك فقط.</summary>
    [HttpDelete("{id:int}")]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        var deleted = await _reminders.DeleteAsync(id, User.GetUserId(), ActorName, ct);
        return deleted ? NoContent() : NotFound();
    }
}
