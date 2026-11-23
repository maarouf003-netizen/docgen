using DocGenerator.Api.Authorization;
using DocGenerator.Api.Http;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Sections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/sections")]
[Authorize(Roles = "lawyer,head,subhead,manager,admin")]
public class SectionsController : ControllerBase
{
    private readonly ISectionService _sections;
    private readonly IUserCapabilities _capabilities;
    private readonly ICommandHandler<CreateSectionCommand, SectionDto> _createSection;
    private readonly ICommandHandler<DeleteSectionCommand, bool> _deleteSection;

    public SectionsController(
        ISectionService sections,
        IUserCapabilities capabilities,
        ICommandHandler<CreateSectionCommand, SectionDto> createSection,
        ICommandHandler<DeleteSectionCommand, bool> deleteSection)
    {
        _sections = sections;
        _capabilities = capabilities;
        _createSection = createSection;
        _deleteSection = deleteSection;
    }

    private string? ActorName => User.Identity?.Name;
    private bool CanManage => _capabilities.Has(User, UserCapability.CanManageBranches);

    /// <summary>قائمة شعب فرع — كل الأدوار المصادَّقة (للمنسدلات والإدارة)؛ الفرع إلزامي صراحةً.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? branchId, CancellationToken ct)
    {
        if (branchId is null)
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.ValidationFailed,
                "حدد الفرع لعرض شعبه",
                field: "branchId");
        return Ok(await _sections.ListSectionsAsync(branchId.Value, ct));
    }

    /// <summary>إنشاء شعبة في فرع — مشرف/مدير (قرار §2.15).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSectionRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var section = await _createSection.HandleAsync(
                new CreateSectionCommand(request.BranchId, request.Name, ActorName), ct);
            return CreatedAtAction(nameof(Get), new { id = section.Id }, section);
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
            var section = await _sections.RenameSectionAsync(id, request.Name, ActorName, request.Version, ct);
            return section is null ? NotFound() : Ok(section);
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

    /// <summary>تفعيل/تعطيل شعبة — مشرف/مدير (التعطيل ممنوع مع وجود دوائر).</summary>
    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetSectionActiveRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        var section = await _sections.SetActiveAsync(id, request.IsActive, ActorName, request.Version, ct);
        return section is null ? NotFound() : Ok(section);
    }

    /// <summary>حذف شعبة — مشرف/مدير (ممنوع مع وجود دوائر أو حسابات).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        return await _deleteSection.HandleAsync(
            new DeleteSectionCommand(id, ActorName), ct) ? NoContent() : NotFound();
    }
}
