using DocGenerator.Api.Authorization;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/branches")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IBranchManagementService _branches;

    public BranchesController(IBranchManagementService branches)
    {
        _branches = branches;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool CanManageBranches => RolePermissions.CanManageBranches(Role);

    /// <summary>قائمة الفروع — كل الأدوار المصادَّقة (للقوائم المنسدلة والإدارة).</summary>
    [HttpGet]
    public async Task<ActionResult<List<BranchDto>>> List(CancellationToken ct)
    {
        return Ok(await _branches.ListBranchesAsync(ct));
    }

    /// <summary>إنشاء فرع — المشرف فقط.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequest request, CancellationToken ct)
    {
        if (!CanManageBranches)
            return Forbid();
        try
        {
            var branch = await _branches.CreateBranchAsync(request, ActorName, ct);
            return CreatedAtAction(nameof(Get), new { id = branch.Id }, branch);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var branch = (await _branches.ListBranchesAsync(ct)).FirstOrDefault(b => b.Id == id);
        return branch is null ? NotFound() : Ok(branch);
    }

    /// <summary>تعديل فرع (بما فيه التفعيل/التعطيل) — المشرف فقط.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBranchRequest request, CancellationToken ct)
    {
        if (!CanManageBranches)
            return Forbid();
        try
        {
            var branch = await _branches.UpdateBranchAsync(id, request, ActorName, ct);
            return branch is null ? NotFound() : Ok(branch);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>
    /// حذف فرع نهائياً — المشرف فقط، والفروع المستخدمة (مستخدمون/مستندات) تُرفض
    /// وتعطَّل بدلاً من الحذف.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!CanManageBranches)
            return Forbid();
        try
        {
            var ok = await _branches.DeleteBranchAsync(id, ActorName, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }
}
