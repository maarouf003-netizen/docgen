using DocGenerator.Api.Authorization;
using DocGenerator.Api.Http;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.ReviewLetters;
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
    private readonly IUserCapabilities _capabilities;
    private readonly IQueryHandler<SearchReviewLettersQuery, PagedResult<ReviewLetterListItemDto>> _searchLetters;
    private readonly IQueryHandler<GetReviewLetterAdministrativeBranchesQuery, List<string>> _administrativeBranches;
    private readonly IQueryHandler<CountPendingReviewLettersQuery, int> _pendingCount;
    private readonly IQueryHandler<CountUnseenReviewRepliesQuery, int> _unseenRepliesCount;
    private readonly IQueryHandler<ListReviewLettersByDocumentQuery, List<ReviewLetterListItemDto>> _lettersByDocument;
    private readonly IQueryHandler<GetReviewLetterQuery, ReviewLetterDto> _getLetter;
    private readonly ICommandHandler<MarkReviewRepliesSeenCommand, bool> _markRepliesSeen;
    private readonly ICommandHandler<AddReviewLetterAddendumCommand, ReviewLetterMessageDto> _addAddendum;
    private readonly ICommandHandler<ReplyReviewLetterCommand, ReviewLetterMessageDto> _reply;
    private readonly ICommandHandler<CreateReviewLetterCommand, ReviewLetterDto> _createLetter;

    public ReviewLettersController(
        IUserCapabilities capabilities,
        IQueryHandler<SearchReviewLettersQuery, PagedResult<ReviewLetterListItemDto>> searchLetters,
        IQueryHandler<GetReviewLetterAdministrativeBranchesQuery, List<string>> administrativeBranches,
        IQueryHandler<CountPendingReviewLettersQuery, int> pendingCount,
        IQueryHandler<CountUnseenReviewRepliesQuery, int> unseenRepliesCount,
        IQueryHandler<ListReviewLettersByDocumentQuery, List<ReviewLetterListItemDto>> lettersByDocument,
        IQueryHandler<GetReviewLetterQuery, ReviewLetterDto> getLetter,
        ICommandHandler<MarkReviewRepliesSeenCommand, bool> markRepliesSeen,
        ICommandHandler<AddReviewLetterAddendumCommand, ReviewLetterMessageDto> addAddendum,
        ICommandHandler<ReplyReviewLetterCommand, ReviewLetterMessageDto> reply,
        ICommandHandler<CreateReviewLetterCommand, ReviewLetterDto> createLetter)
    {
        _capabilities = capabilities;
        _searchLetters = searchLetters;
        _administrativeBranches = administrativeBranches;
        _pendingCount = pendingCount;
        _unseenRepliesCount = unseenRepliesCount;
        _lettersByDocument = lettersByDocument;
        _getLetter = getLetter;
        _markRepliesSeen = markRepliesSeen;
        _addAddendum = addAddendum;
        _reply = reply;
        _createLetter = createLetter;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool IsLawyer => _capabilities.Has(User, UserCapability.IsLawyer);
    private bool IsHeadOrSubHead => _capabilities.Has(User, UserCapability.IsHeadOrSubHead);
    private bool IsSubHead => _capabilities.Has(User, UserCapability.IsSubHead);
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
        if (IsSubHead && SectionId is null)
            return Forbid();
        var result = await _searchLetters.HandleAsync(
            new SearchReviewLettersQuery(UserId, Role, BranchId, q, page, perPage,
                administrativeBranch, SectionId), ct);
        return Ok(result);
    }

    /// <summary>خيارات فلتر «فرع الإدارة» المميزة — مدير/مشرف فقط.</summary>
    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanSeeAdministrativeBranch))
            return Ok(new { administrativeBranches = new List<string>() });

        var branches = await _administrativeBranches.HandleAsync(
            new GetReviewLetterAdministrativeBranchesQuery(), ct);
        return Ok(new { administrativeBranches = branches });
    }

    /// <summary>عدد كتب النطاق بانتظار الرد — جرس المالك الأحمر (§10).</summary>
    [HttpGet("pending-count")]
    public async Task<IActionResult> PendingCount(CancellationToken ct)
    {
        if (!IsHeadOrSubHead)
            return Forbid();
        if (BranchId is null)
            // عدّاد قراءة (D11): 403 لا 400 — لا نطاق = لا صلاحية.
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status403Forbidden,
                ApiErrorCodes.BranchRequired,
                "الرئيس دون فرع");
        if (IsSubHead && SectionId is null)
            return Forbid();

        var count = await _pendingCount.HandleAsync(
            new CountPendingReviewLettersQuery(BranchId.Value, SectionId), ct);
        return Ok(new { count });
    }

    /// <summary>عدد كتب المحامي فيها ردّ لم يطّلع عليه — شارة بند المطالعات.</summary>
    [HttpGet("unseen-replies-count")]
    public async Task<IActionResult> UnseenRepliesCount(CancellationToken ct)
    {
        if (!IsLawyer)
            return Forbid();

        var count = await _unseenRepliesCount.HandleAsync(
            new CountUnseenReviewRepliesQuery(UserId), ct);
        return Ok(new { count });
    }

    /// <summary>تعليم ردود الكتاب كمطّلع عليها — محامي الكتاب عند فتحه إياه.</summary>
    [HttpPost("{id:int}/mark-replies-seen")]
    public async Task<IActionResult> MarkRepliesSeen(int id, CancellationToken ct)
    {
        if (!IsLawyer)
            return Forbid();

        await _markRepliesSeen.HandleAsync(new MarkReviewRepliesSeenCommand(id, UserId), ct);
        return NoContent();
    }

    /// <summary>كتب ملف محدد — لمالك الملف ورئيس نطاقه والإدارة ومتابعيه.</summary>
    [HttpGet("document/{documentId:int}")]
    public async Task<IActionResult> ListByDocument(int documentId, CancellationToken ct)
    {
        var items = await _lettersByDocument.HandleAsync(
            new ListReviewLettersByDocumentQuery(documentId, UserId, Role, BranchId, SectionId), ct);
        return Ok(items);
    }

    /// <summary>تسطير كتاب مطالعة — المحامي فقط.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewLetterRequest request,
        CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanCreateReviewLetters))
            return Forbid();
        if (BranchId is null)
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.BranchRequired,
                "المحامي دون فرع لا يمكنه تسطير مطالعات");

        var letter = await _createLetter.HandleAsync(
            new CreateReviewLetterCommand(request, UserId, ActorName, BranchId.Value), ct);
        return CreatedAtAction(nameof(Get), new { id = letter.Id }, letter);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var letter = await _getLetter.HandleAsync(
            new GetReviewLetterQuery(id, UserId, Role, BranchId, SectionId), ct);
        return Ok(letter);
    }

    /// <summary>إضافة لاحق إلى كتاب — محامي الكتاب فقط.</summary>
    [HttpPost("{id:int}/addenda")]
    public async Task<IActionResult> AddAddendum(int id,
        [FromBody] AddReviewLetterAddendumRequest request, CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanCreateReviewLetters))
            return Forbid();

        var addendum = await _addAddendum.HandleAsync(
            new AddReviewLetterAddendumCommand(id, request, UserId, ActorName), ct);
        return Ok(addendum);
    }

    /// <summary>رد مالك الكتاب على كتاب — قسمه لدوائر القسم وبلا دائرة، وشعبته لدوائر شعبته (§10).</summary>
    [HttpPost("{id:int}/replies")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyReviewLetterRequest request,
        CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanReplyReviewLetters))
            return Forbid();
        if (BranchId is null)
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.BranchRequired,
                "الرئيس دون فرع لا يمكنه الرد على المطالعات");
        if (IsSubHead && SectionId is null)
            return Forbid();

        var reply = await _reply.HandleAsync(
            new ReplyReviewLetterCommand(id, request, UserId, ActorName, BranchId.Value, Role, SectionId), ct);
        return Ok(reply);
    }
}
