using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Branches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/branches")]
[Authorize(Roles = "lawyer,head,subhead,manager,admin")]
public class BranchesController : ControllerBase
{
    private readonly IBranchManagementService _branches;
    private readonly IUserCapabilities _capabilities;
    private readonly ICommandHandler<DeleteBranchCommand, bool> _deleteBranch;
    private readonly ICommandHandler<CreateBranchCommand, BranchDto> _createBranch;

    public BranchesController(
        IBranchManagementService branches,
        IUserCapabilities capabilities,
        ICommandHandler<DeleteBranchCommand, bool> deleteBranch,
        ICommandHandler<CreateBranchCommand, BranchDto> createBranch)
    {
        _branches = branches;
        _capabilities = capabilities;
        _deleteBranch = deleteBranch;
        _createBranch = createBranch;
    }

    private string? ActorName => User.Identity?.Name;

    private bool CanManageBranches => _capabilities.Has(User, UserCapability.CanManageBranches);

    /// <summary>قائمة الفروع — كل الأدوار المصادَّقة (للقوائم المنسدلة والإدارة).</summary>
    [HttpGet]
    public async Task<ActionResult<List<BranchDto>>> List(CancellationToken ct)
    {
        return Ok(await _branches.ListBranchesAsync(ct));
    }

    /// <summary>إنشاء فرع — المشرف والمدير (قرار §2.15).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequest request, CancellationToken ct)
    {
        if (!CanManageBranches)
            return Forbid();
        var branch = await _createBranch.HandleAsync(new CreateBranchCommand(request, ActorName), ct);
        return CreatedAtAction(nameof(Get), new { id = branch.Id }, branch);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var branch = (await _branches.ListBranchesAsync(ct)).FirstOrDefault(b => b.Id == id);
        return branch is null ? NotFound() : Ok(branch);
    }

    /// <summary>تعديل فرع (بما فيه التفعيل/التعطيل) — المشرف والمدير (قرار §2.15).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBranchRequest request, CancellationToken ct)
    {
        if (!CanManageBranches)
            return Forbid();
        var branch = await _branches.UpdateBranchAsync(id, request, ActorName, ct);
        return branch is null ? NotFound() : Ok(branch);
    }

    /// <summary>
    /// حذف فرع نهائياً — المشرف والمدير (قرار §2.15)، والفروع المستخدمة (مستخدمون/مستندات) تُرفض
    /// وتعطَّل بدلاً من الحذف.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!CanManageBranches)
            return Forbid();
        var ok = await _deleteBranch.HandleAsync(new DeleteBranchCommand(id, ActorName), ct);
        return ok ? NoContent() : NotFound();
    }
}
