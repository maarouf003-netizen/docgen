using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// كتب المطالعة: المحامي يسطّر (موجَّهًا لمالك الدائرة تلقائيًا مع ملف، وباختيار
/// المنسدل بلا ملف)، ويردّ مالك الكتاب (قسمه/شعبته §10)، والاطلاع موسّع لمالك
/// الملف ومتابعيه (إحالة/إنابة/استئناف) وللمدير والمشرف (قراءة فقط بفرع إدارة منتقى).
/// </summary>
[ApiController]
[Route("api/review-letters")]
[Authorize(Roles = "lawyer,head,subhead,manager,admin")]
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
    private bool IsHeadOrSubHead => RolePermissions.IsHeadOrSubHead(Role);
    private int? BranchId => User.GetBranchId();
    private int? SectionId => User.GetSectionId();
    private int UserId => User.GetUserId();

    /// <summary>قائمة كتب المطالعة بحسب الدور، مع بحث نصي وترقيم؛ المدير/المشرف بفرع إدارة إجباري.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] string? administrativeBranch,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        if (Role == UserRole.SubHead && SectionId is null)
            return Forbid();
        try
        {
            var result = await _letters.SearchAsync(UserId, Role, BranchId, q, page, perPage,
                administrativeBranch, ct, SectionId);
            return Ok(result);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>خيارات فلتر «فرع الإدارة» المميزة — مدير/مشرف فقط.</summary>
    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(CancellationToken ct)
    {
        if (!RolePermissions.CanSeeAdministrativeBranch(Role))
            return Ok(new { administrativeBranches = new List<string>() });

        var branches = await _letters.GetAdministrativeBranchesAsync(ct);
        return Ok(new { administrativeBranches = branches });
    }

    /// <summary>عدد كتب النطاق بانتظار الرد — جرس المالك الأحمر (§10).</summary>
    [HttpGet("pending-count")]
    public async Task<IActionResult> PendingCount(CancellationToken ct)
    {
        if (!IsHeadOrSubHead)
            return Forbid();
        if (BranchId is null)
            return BadRequest(new { message = "الرئيس دون فرع" });
        if (Role == UserRole.SubHead && SectionId is null)
            return Forbid();

        var count = await _letters.CountPendingForHeadAsync(BranchId.Value, SectionId, ct);
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

    /// <summary>كتب ملف محدد — لمالك الملف ورئيس نطاقه والإدارة ومتابعيه.</summary>
    [HttpGet("document/{documentId:int}")]
    public async Task<IActionResult> ListByDocument(int documentId, CancellationToken ct)
    {
        try
        {
            var items = await _letters.ListByDocumentAsync(documentId, UserId, Role, BranchId, ct, SectionId);
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
            var letter = await _letters.GetByIdAsync(id, UserId, Role, BranchId, ct, SectionId);
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

    /// <summary>رد مالك الكتاب على كتاب — قسمه لدوائر القسم وبلا دائرة، وشعبته لدوائر شعبته (§10).</summary>
    [HttpPost("{id:int}/replies")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyReviewLetterRequest request,
        CancellationToken ct)
    {
        if (!RolePermissions.CanReplyReviewLetters(Role))
            return Forbid();
        if (BranchId is null)
            return BadRequest(new { message = "الرئيس دون فرع لا يمكنه الرد على المطالعات" });
        if (Role == UserRole.SubHead && SectionId is null)
            return Forbid();

        try
        {
            var reply = await _letters.ReplyAsync(id, request, UserId, ActorName, BranchId.Value, ct, Role, SectionId);
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
