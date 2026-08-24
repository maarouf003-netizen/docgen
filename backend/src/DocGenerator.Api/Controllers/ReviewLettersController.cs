using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// كتب المطالعة: المحامي يسطّر ويردّ رئيس القسم، والاطلاع موسّع لمالك الملف
/// ومتابعيه (إحالة/إنابة/استئناف) وللمدير والمشرف (قراءة فقط).
/// </summary>
[ApiController]
[Route("api/review-letters")]
[Authorize]
public class ReviewLettersController : ControllerBase
{
    private readonly IReviewLetterService _letters;

    public ReviewLettersController(IReviewLetterService letters)
    {
        _letters = letters;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool IsLawyer => Role == UserRole.Lawyer;
    private bool IsHead => Role == UserRole.Head;
    private int? BranchId => User.GetBranchId();
    private int UserId => User.GetUserId();

    /// <summary>قائمة كتب المطالعة بحسب الدور، مع بحث نصي وترقيم.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _letters.SearchAsync(UserId, Role, BranchId, q, page, perPage, ct);
            return Ok(result);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>عدد كتب الفرع بانتظار الرد — جرس رئيس القسم الأحمر.</summary>
    [HttpGet("pending-count")]
    public async Task<IActionResult> PendingCount(CancellationToken ct)
    {
        if (!IsHead)
            return Forbid();
        if (BranchId is null)
            return BadRequest(new { message = "رئيس القسم دون فرع" });

        var count = await _letters.CountPendingForHeadAsync(BranchId.Value, ct);
        return Ok(new { count });
    }

    /// <summary>عدد كتب المحامي فيها ردّ لم يطّلع عليه — شارة بند المطالعات.</summary>
    [HttpGet("unseen-replies-count")]
    public async Task<IActionResult> UnseenRepliesCount(CancellationToken ct)
    {
        if (!IsLawyer)
            return Forbid();

        var count = await _letters.CountUnseenRepliesForLawyerAsync(UserId, ct);
        return Ok(new { count });
    }

    /// <summary>تعليم ردود الكتاب كمطّلع عليها — محامي الكتاب عند فتحه إياه.</summary>
    [HttpPost("{id:int}/mark-replies-seen")]
    public async Task<IActionResult> MarkRepliesSeen(int id, CancellationToken ct)
    {
        if (!IsLawyer)
            return Forbid();

        try
        {
            await _letters.MarkRepliesSeenAsync(id, UserId, ct);
            return NoContent();
        }
        catch (ArgumentException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>كتب ملف محدد — لمالك الملف ورئيس قسمه والإدارة ومتابعيه.</summary>
    [HttpGet("document/{documentId:int}")]
    public async Task<IActionResult> ListByDocument(int documentId, CancellationToken ct)
    {
        try
        {
            var items = await _letters.ListByDocumentAsync(documentId, UserId, Role, BranchId, ct);
            return Ok(items);
        }
        catch (ArgumentException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>تسطير كتاب مطالعة — المحامي فقط.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewLetterRequest request,
        CancellationToken ct)
    {
        if (!RolePermissions.CanCreateReviewLetters(Role))
            return Forbid();
        if (BranchId is null)
            return BadRequest(new { message = "المحامي دون فرع لا يمكنه تسطير مطالعات" });

        try
        {
            var letter = await _letters.CreateAsync(request, UserId, ActorName, BranchId.Value, ct);
            return CreatedAtAction(nameof(Get), new { id = letter.Id }, letter);
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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        try
        {
            var letter = await _letters.GetByIdAsync(id, UserId, Role, BranchId, ct);
            return Ok(letter);
        }
        catch (ArgumentException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>إضافة لاحق إلى كتاب — محامي الكتاب فقط.</summary>
    [HttpPost("{id:int}/addenda")]
    public async Task<IActionResult> AddAddendum(int id,
        [FromBody] AddReviewLetterAddendumRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanCreateReviewLetters(Role))
            return Forbid();

        try
        {
            var addendum = await _letters.AddAddendumAsync(id, request, UserId, ActorName, ct);
            return Ok(addendum);
        }
        catch (ArgumentException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>رد رئيس القسم على كتاب — رئيس قسم الفرع نفسه فقط.</summary>
    [HttpPost("{id:int}/replies")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyReviewLetterRequest request,
        CancellationToken ct)
    {
        if (!RolePermissions.CanReplyReviewLetters(Role))
            return Forbid();
        if (BranchId is null)
            return BadRequest(new { message = "رئيس القسم دون فرع لا يمكنه الرد على المطالعات" });

        try
        {
            var reply = await _letters.ReplyAsync(id, request, UserId, ActorName, BranchId.Value, ct);
            return Ok(reply);
        }
        catch (ArgumentException e)
        {
            return NotFound(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
