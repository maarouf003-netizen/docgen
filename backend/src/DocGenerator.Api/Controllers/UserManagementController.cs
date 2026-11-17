using DocGenerator.Api.Authorization;
using DocGenerator.Api.Http;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "head,subhead,manager,admin")]
public class UserManagementController : ControllerBase
{
    private readonly IUserManagementService _users;
    private readonly IUserCapabilities _capabilities;

    public UserManagementController(
        IUserManagementService users, IUserCapabilities capabilities)
    {
        _users = users;
        _capabilities = capabilities;
    }

    private string? ActorName => User.Identity?.Name;

    private bool IsHeadOrSubHead => _capabilities.Has(User, UserCapability.IsHeadOrSubHead);
    private bool CanManageBranchLawyers =>
        _capabilities.Has(User, UserCapability.CanManageBranchLawyers);
    private bool CanManageUsers => _capabilities.Has(User, UserCapability.CanManageUsers);

    [HttpGet]
    public async Task<IActionResult> ListUsers(CancellationToken ct)
    {
        // إدارة المستخدمين — مشرف (الكل) ومدير (عدا حسابات المشرف — `BQ-001د`).
        if (!CanManageUsers)
            return Forbid();
        return Ok(await _users.ListUsersAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        if (!CanManageUsers)
            return Forbid();
        // حد المشرف يُفرَض في الخدمة بمعرفة الفاعل (`BQ-001د`).
        var user = await _users.CreateUserAsync(request, ActorName, ct, User.GetUserId());
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
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
        var user = await _users.UpdateUserAsync(id, request, User.GetUserId(), ActorName, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("lawyers")]
    public async Task<IActionResult> ListLawyers(
        [FromQuery] int? branchId,
        [FromQuery] string? mode,
        [FromQuery] bool includeInactive = true,
        CancellationToken ct = default)
    {
        // إدارة محامي الفرع — رئيس القسم والشعبة (فرعه) والمشرف/المدير (أي فرع).
        // الوضعان خادميًا (§5.4): `mine` (الافتراضي) و`branch` للنوافذ.
        if (!CanManageBranchLawyers)
            return Forbid();

        int? effectiveBranchId;
        if (IsHeadOrSubHead)
        {
            effectiveBranchId = User.GetBranchId();
            if (effectiveBranchId is null)
                return Forbid();
        }
        else
        {
            effectiveBranchId = branchId;
        }

        return Ok(await _users.ListLawyersAsync(
            effectiveBranchId, mode, User.GetUserId(), User.GetSectionId(),
            IsHeadOrSubHead, includeInactive, ct));
    }

    [HttpPost("lawyers")]
    public async Task<IActionResult> CreateLawyer([FromBody] CreateLawyerRequest request, CancellationToken ct)
    {
        if (!CanManageBranchLawyers)
            return Forbid();

        // رئيس القسم والشعبة يضيف لمحامي فرعه فقط؛ المشرف يحدد الفرع في الطلب.
        int? effectiveBranchId;
        if (IsHeadOrSubHead)
        {
            effectiveBranchId = User.GetBranchId();
            if (effectiveBranchId is null)
                return Forbid();
        }
        else
        {
            effectiveBranchId = request.BranchId;
            if (effectiveBranchId is null)
                return ApiProblem.Result(
                    HttpContext,
                    StatusCodes.Status400BadRequest,
                    ApiErrorCodes.ValidationFailed,
                    "يجب تحديد الفرع",
                    field: "branchId");
        }

        var lawyer = await _users.CreateLawyerAsync(effectiveBranchId.Value, request, ActorName, ct, User.GetUserId());
        return Ok(lawyer);
    }

    [HttpPut("lawyers/{id:int}")]
    public async Task<IActionResult> UpdateLawyer(int id, [FromBody] UpdateLawyerRequest request, CancellationToken ct)
    {
        if (!CanManageBranchLawyers)
            return Forbid();

        // نطاق رئيس القسم والشعبة محصور بمحامي فرعه؛ المشرف بلا نطاق.
        var scopeBranchId = IsHeadOrSubHead ? User.GetBranchId() : (int?)null;
        if (IsHeadOrSubHead && scopeBranchId is null)
            return Forbid();

        var lawyer = await _users.UpdateLawyerAsync(id, request, scopeBranchId, ActorName, ct);
        return lawyer is null ? NotFound() : Ok(lawyer);
    }

    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetUserActiveRequest request, CancellationToken ct)
    {
        if (!CanManageBranchLawyers)
            return Forbid();

        // نطاق رئيس القسم والشعبة محصور بمحامي فرعه؛ المشرف بلا نطاق.
        var scopeBranchId = IsHeadOrSubHead ? User.GetBranchId() : (int?)null;
        if (IsHeadOrSubHead && scopeBranchId is null)
            return Forbid();

        var ok = await _users.SetLawyerActiveAsync(id, request.IsActive, scopeBranchId, ActorName, ct);
        if (!ok)
            return NotFound();
        return Ok(new { message = "تم تحديث حالة المحامي" });
    }
}
