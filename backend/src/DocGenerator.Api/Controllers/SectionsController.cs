using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/sections")]
[Authorize(Roles = "lawyer,head,subhead,manager,admin")]
public class SectionsController : ControllerBase
{
    private readonly ISectionService _sections;

    public SectionsController(ISectionService sections)
    {
        _sections = sections;
    }

    private string? ActorName => User.Identity?.Name;
    private UserRole Role => User.GetRoleEnum();
    private bool CanManage => RolePermissions.CanManageBranches(Role);

    /// <summary>قائمة شعب فرع — كل الأدوار المصادَّقة (للمنسدلات والإدارة)؛ الفرع إلزامي صراحةً.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? branchId, CancellationToken ct)
    {
        if (branchId is null)
            return BadRequest(new { message = "حدد الفرع لعرض شعبه" });
        try
        {
            return Ok(await _sections.ListSectionsAsync(branchId.Value, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>إنشاء شعبة في فرع — مشرف/مدير (قرار §2.15).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSectionRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var section = await _sections.CreateSectionAsync(request.BranchId, request.Name, ActorName, ct);
            return CreatedAtAction(nameof(Get), new { id = section.Id }, section);
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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var section = await _sections.GetSectionAsync(id, ct);
        return section is null ? NotFound() : Ok(section);
    }

    /// <summary>إعادة تسمية شعبة — مشرف/مدير.</summary>
    [HttpPut("{id:int}/rename")]
    public async Task<IActionResult> Rename(int id, [FromBody] RenameSectionRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var section = await _sections.RenameSectionAsync(id, request.Name, ActorName, ct);
            return section is null ? NotFound() : Ok(section);
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

    /// <summary>تفعيل/تعطيل شعبة — مشرف/مدير (التعطيل ممنوع مع وجود دوائر).</summary>
    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetSectionActiveRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var section = await _sections.SetActiveAsync(id, request.IsActive, ActorName, ct);
            return section is null ? NotFound() : Ok(section);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>حذف شعبة — مشرف/مدير (ممنوع مع وجود دوائر أو حسابات).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            return await _sections.DeleteSectionAsync(id, ActorName, ct) ? NoContent() : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }
}
