using DocGenerator.Api.Security;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// منتدى المحامين (تيار واحد مسطّح عابر للفروع عمدًا): نشر نصي + اقتباس +
/// تثبيت المشرف + «شوهدت بواسطة» للكاتب + عدّاد غير المقروء للشارة.
/// المندوب مستبعد بنيويًا (حارس البوابة + سمة الأدوار + تحقق الخدمة).
/// </summary>
[ApiController]
[Route("api/forum")]
[Authorize(Roles = "lawyer,head,subhead,manager,admin")]
public class ForumController : ControllerBase
{
    private readonly IForumService _forum;

    public ForumController(IForumService forum)
    {
        _forum = forum;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private int? SectionId => User.GetSectionId();
    private int UserId => User.GetUserId();

    /// <summary>رئيس الشعبة بلا شعبة جلسة مكسورة — تُرفض الكتابة ككل المسارات.</summary>
    private bool SubHeadSessionBroken => Role == UserRole.SubHead && SectionId is null;

    /// <summary>شريحة التيار بمفتاح `Id` (الأحدث أولًا بلا مؤشر) + بحث النص.</summary>
    [HttpGet("messages")]
    public async Task<IActionResult> List(
        [FromQuery] int limit = 30, [FromQuery] int? before = null,
        [FromQuery] int? after = null, [FromQuery] string? q = null,
        CancellationToken ct = default)
        => Ok(await _forum.ListAsync(UserId, limit, before, after, q, ct));

    /// <summary>رسالة واحدة بالمعرف.</summary>
    [HttpGet("messages/{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var dto = await _forum.GetByIdAsync(id, UserId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>الرسالة المثبّتة للشريط (`204` عند عدمها).</summary>
    [HttpGet("pinned")]
    public async Task<IActionResult> GetPinned(CancellationToken ct)
    {
        var dto = await _forum.GetPinnedAsync(UserId, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    /// <summary>نشر رسالة (مع اقتباس اختياري) — الهوية تُلقط خلفيًا لا من العميل.</summary>
    [HttpPost("messages")]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    public async Task<IActionResult> Post([FromBody] PostForumMessageRequest request, CancellationToken ct)
    {
        if (SubHeadSessionBroken)
            return Forbid();
        try
        {
            var message = await _forum.PostAsync(request, UserId, ct);
            return CreatedAtAction(nameof(Get), new { id = message.Id }, message);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>تعديل رسالة — الكاتب فقط (يختم «عُدّل»).</summary>
    [HttpPut("messages/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateForumMessageRequest request, CancellationToken ct)
    {
        if (SubHeadSessionBroken)
            return Forbid();
        try
        {
            return Ok(await _forum.UpdateAsync(id, request, UserId, ct));
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>حذف صلب — الكاتب لرسالته والمشرف لأي رسالة (مع إيصالاتها).</summary>
    [HttpDelete("messages/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (SubHeadSessionBroken)
            return Forbid();
        try
        {
            await _forum.DeleteAsync(id, UserId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>تبديل التثبيت — المشرف فقط (الجديد يُسقط السابق).</summary>
    [HttpPost("messages/{id:int}/pin")]
    public async Task<IActionResult> TogglePin(int id, CancellationToken ct)
    {
        if (SubHeadSessionBroken)
            return Forbid();
        // التثبيت للمشرف حصرًا (مصفوفة §2-أ — المدير قارئ هنا لا مثبّت).
        if (Role != UserRole.Admin)
            return Forbid();
        try
        {
            return Ok(await _forum.TogglePinAsync(id, UserId, ct));
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
    }

    /// <summary>تعليم القراءة حتى رسالة — عدّادي بلا تراجع.</summary>
    [HttpPost("read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkForumReadRequest request, CancellationToken ct)
    {
        if (SubHeadSessionBroken)
            return Forbid();
        try
        {
            var max = await _forum.MarkReadAsync(UserId, ActorName, request.UpToMessageId, ct);
            return Ok(new { maxReadMessageId = max });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>نافذة «شوهدت بواسطة» — الكاتب فقط.</summary>
    [HttpGet("messages/{id:int}/readers")]
    public async Task<IActionResult> GetReaders(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _forum.GetReadersAsync(id, UserId, ct));
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>العدّاد — رسائل بلا إيصال لي (شارة أيقونة المنتدى).</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        try
        {
            var count = await _forum.CountUnreadAsync(UserId, ct);
            return Ok(new { count });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
