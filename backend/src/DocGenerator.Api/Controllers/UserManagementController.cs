using DocGenerator.Api.Authorization;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UserManagementController : ControllerBase
{
    private readonly IUserManagementService _users;

    public UserManagementController(IUserManagementService users)
    {
        _users = users;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool IsHead => Role == UserRole.Head;
    private bool CanManageBranchLawyers => RolePermissions.CanManageBranchLawyers(Role);
    private bool CanManageUsers => RolePermissions.CanManageUsers(Role);

    [HttpGet]
    public async Task<IActionResult> ListUsers(CancellationToken ct)
    {
        // إدارة المستخدمين الكاملة — المشرف فقط.
        if (!CanManageUsers)
            return Forbid();
        return Ok(await _users.ListUsersAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        if (!CanManageUsers)
            return Forbid();
        try
        {
            var user = await _users.CreateUserAsync(request, ActorName, ct);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id, CancellationToken ct)
    {
        if (!CanManageUsers)
            return Forbid();
        var user = (await _users.ListUsersAsync(ct)).FirstOrDefault(u => u.Id == id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        if (!CanManageUsers)
            return Forbid();
        try
        {
            var user = await _users.UpdateUserAsync(id, request, User.GetUserId(), ActorName, ct);
            return user is null ? NotFound() : Ok(user);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpGet("lawyers")]
    public async Task<IActionResult> ListLawyers([FromQuery] int? branchId, CancellationToken ct)
    {
        // إدارة محامي الفرع — رئيس القسم (فرعه) والمشرف (أي فرع).
        if (!CanManageBranchLawyers)
            return Forbid();

        var effectiveBranchId = IsHead ? User.GetBranchId() : branchId;
        if (IsHead && effectiveBranchId is null)
            return Forbid();

        return Ok(await _users.ListLawyersAsync(effectiveBranchId, ct));
    }

    [HttpPost("lawyers")]
    public async Task<IActionResult> CreateLawyer([FromBody] CreateLawyerRequest request, CancellationToken ct)
    {
        if (!CanManageBranchLawyers)
            return Forbid();

        // رئيس القسم يضيف لمحامي فرعه فقط؛ المشرف يحدد الفرع في الطلب.
        int? effectiveBranchId;
        if (IsHead)
        {
            effectiveBranchId = User.GetBranchId();
            if (effectiveBranchId is null)
                return Forbid();
        }
        else
        {
            effectiveBranchId = request.BranchId;
            if (effectiveBranchId is null)
                return BadRequest(new { message = "يجب تحديد الفرع" });
        }

        try
        {
            var lawyer = await _users.CreateLawyerAsync(effectiveBranchId.Value, request, ActorName, ct);
            return Ok(lawyer);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetUserActiveRequest request, CancellationToken ct)
    {
        if (!CanManageBranchLawyers)
            return Forbid();

        // نطاق رئيس القسم محصور بمحامي فرعه؛ المشرف بلا نطاق.
        var scopeBranchId = IsHead ? User.GetBranchId() : (int?)null;
        if (IsHead && scopeBranchId is null)
            return Forbid();

        var ok = await _users.SetLawyerActiveAsync(id, request.IsActive, scopeBranchId, ActorName, ct);
        if (!ok)
            return NotFound();
        return Ok(new { message = "تم تحديث حالة المحامي" });
    }
}
