using DocGenerator.Api.Authorization;
using DocGenerator.Api.Security;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Suggestions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/app-suggestions")]
[Authorize(Roles = "lawyer,head,subhead,manager,admin")]
public class AppSuggestionsController : ControllerBase
{
    private readonly IAppSuggestionService _suggestions;
    private readonly IUserCapabilities _capabilities;
    private readonly ICommandHandler<MarkAppSuggestionReadCommand, bool> _markRead;
    private readonly ICommandHandler<CreateAppSuggestionCommand, AppSuggestionDto> _createSuggestion;

    public AppSuggestionsController(
        IAppSuggestionService suggestions,
        IUserCapabilities capabilities,
        ICommandHandler<MarkAppSuggestionReadCommand, bool> markRead,
        ICommandHandler<CreateAppSuggestionCommand, AppSuggestionDto> createSuggestion)
    {
        _suggestions = suggestions;
        _capabilities = capabilities;
        _markRead = markRead;
        _createSuggestion = createSuggestion;
    }

    private string? ActorName => User.Identity?.Name;

    /// <summary>صندوق المشرف/المدير (`CanViewAppSuggestions` عبر جسر القدرات).</summary>
    private bool IsAdmin => _capabilities.Has(User, UserCapability.CanViewAppSuggestions);

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

    /// <summary>إرسال اقتراح تطوير — المحامي ورئيس القسم (صندوق المشرف).</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    public async Task<IActionResult> Create([FromBody] CreateAppSuggestionRequest request, CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanSuggestApp))
            return Forbid();
        var suggestion = await _createSuggestion.HandleAsync(
            new CreateAppSuggestionCommand(request.Message, User.GetUserId(), ActorName), ct);
        return CreatedAtAction(nameof(Get), new { id = suggestion.Id }, suggestion);
    }

    /// <summary>عدد الاقتراحات غير المقروءة — شارة بطاقة «اقتراحات التطوير» (مشرف/مدير).</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        if (!IsAdmin)
            return Forbid();
        return Ok(new { count = await _suggestions.CountUnreadAsync(ct) });
    }

    /// <summary>تعليم الاقتراح مقروءًا — مشرف ومدير (`BQ-001`).</summary>
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        if (!IsAdmin)
            return Forbid();
        var ok = await _markRead.HandleAsync(new MarkAppSuggestionReadCommand(id, ActorName), ct);
        return ok ? NoContent() : NotFound();
    }
}
