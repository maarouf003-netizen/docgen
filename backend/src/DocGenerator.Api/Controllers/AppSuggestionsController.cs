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
[Route("api/app-suggestions")]
[Authorize]
public class AppSuggestionsController : ControllerBase
{
    private readonly IAppSuggestionService _suggestions;

    public AppSuggestionsController(IAppSuggestionService suggestions)
    {
        _suggestions = suggestions;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool IsAdmin => RolePermissions.CanViewAppSuggestions(Role);
    private bool IsLawyer => Role == UserRole.Lawyer;

    /// <summary>
    /// قائمة الاقتراحات: المشرف يرى الصندوق مرقّمًا (`page`/`perPage` — بحد أقصى 50)،
    /// وغيره يرى سجل «اقتراحاتي» فقط (كاملًا — سجل شخصي صغير).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        if (IsAdmin)
            return Ok(await _suggestions.ListForAdminAsync(page, perPage, ct));
        return Ok(await _suggestions.ListMineAsync(User.GetUserId(), ct));
    }

    /// <summary>
    /// اقتراح واحد: المشرف يرى أي اقتراح (مع المرسل)، وغيره يرى اقتراحه فقط —
    /// بنفس عقد `List` (الغائب أو غير المملوك = `404` دون كشف الوجود).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var dto = IsAdmin
            ? await _suggestions.GetByIdAsync(id, null, ct)
            : await _suggestions.GetByIdAsync(id, User.GetUserId(), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>إرسال اقتراح تطوير — المحامي الآن (لاحقًا كل الأدوار).</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    public async Task<IActionResult> Create([FromBody] CreateAppSuggestionRequest request, CancellationToken ct)
    {
        if (!IsLawyer)
            return Forbid();
        try
        {
            var suggestion = await _suggestions.CreateAsync(request.Message, User.GetUserId(), ActorName, ct);
            return CreatedAtAction(nameof(Get), new { id = suggestion.Id }, suggestion);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>تعليم الاقتراح مقروءًا — المشرف فقط.</summary>
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        if (!IsAdmin)
            return Forbid();
        var ok = await _suggestions.MarkReadAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
