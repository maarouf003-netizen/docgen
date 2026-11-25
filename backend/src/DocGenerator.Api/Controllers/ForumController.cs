using DocGenerator.Api.Authorization;
using DocGenerator.Api.Http;
using DocGenerator.Api.Security;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Forum;
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
    private readonly IQueryHandler<ListForumMessagesQuery, ForumMessagesPageDto> _listMessages;
    private readonly IQueryHandler<GetForumMessageQuery, ForumMessageDto?> _getMessage;
    private readonly IQueryHandler<GetPinnedForumMessageQuery, ForumMessageDto?> _getPinned;
    private readonly IQueryHandler<GetForumMessageReadersQuery, List<ForumReaderDto>> _getReaders;
    private readonly IQueryHandler<SearchForumUsersQuery, List<ForumUserLookupDto>> _searchUsers;
    private readonly IQueryHandler<CountUnreadForumMessagesQuery, int> _countUnread;
    private readonly IUserCapabilities _capabilities;
    private readonly ICommandHandler<DeleteForumMessageCommand, bool> _deleteMessage;
    private readonly ICommandHandler<ToggleForumPinCommand, ForumMessageDto> _togglePin;
    private readonly ICommandHandler<PostForumMessageCommand, ForumMessageDto> _postMessage;
    private readonly ICommandHandler<UpdateForumMessageCommand, ForumMessageDto> _updateMessage;
    private readonly ICommandHandler<MarkForumReadCommand, int> _markRead;

    public ForumController(
        IQueryHandler<ListForumMessagesQuery, ForumMessagesPageDto> listMessages,
        IQueryHandler<GetForumMessageQuery, ForumMessageDto?> getMessage,
        IQueryHandler<GetPinnedForumMessageQuery, ForumMessageDto?> getPinned,
        IQueryHandler<GetForumMessageReadersQuery, List<ForumReaderDto>> getReaders,
        IQueryHandler<SearchForumUsersQuery, List<ForumUserLookupDto>> searchUsers,
        IQueryHandler<CountUnreadForumMessagesQuery, int> countUnread,
        IUserCapabilities capabilities,
        ICommandHandler<DeleteForumMessageCommand, bool> deleteMessage,
        ICommandHandler<ToggleForumPinCommand, ForumMessageDto> togglePin,
        ICommandHandler<PostForumMessageCommand, ForumMessageDto> postMessage,
        ICommandHandler<UpdateForumMessageCommand, ForumMessageDto> updateMessage,
        ICommandHandler<MarkForumReadCommand, int> markRead)
    {
        _listMessages = listMessages;
        _getMessage = getMessage;
        _getPinned = getPinned;
        _getReaders = getReaders;
        _searchUsers = searchUsers;
        _countUnread = countUnread;
        _capabilities = capabilities;
        _deleteMessage = deleteMessage;
        _togglePin = togglePin;
        _postMessage = postMessage;
        _updateMessage = updateMessage;
        _markRead = markRead;
    }

    private string? ActorName => User.Identity?.Name;

    private int? SectionId => User.GetSectionId();
    private int UserId => User.GetUserId();

    /// <summary>رئيس الشعبة بلا شعبة جلسة مكسورة — تُرفض الكتابة ككل المسارات.</summary>
    private bool SubHeadSessionBroken =>
        _capabilities.Has(User, UserCapability.IsSubHead) && SectionId is null;

    /// <summary>شريحة التيار بمفتاح `Id` (الأحدث أولًا بلا مؤشر).</summary>
    [HttpGet("messages")]
    public async Task<IActionResult> List(
        [FromQuery] int limit = 30, [FromQuery] int? before = null,
        [FromQuery] int? after = null,
        CancellationToken ct = default)
        => Ok(await _listMessages.HandleAsync(
            new ListForumMessagesQuery(UserId, limit, before, after), ct));

    /// <summary>رسالة واحدة بالمعرف.</summary>
    [HttpGet("messages/{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var dto = await _getMessage.HandleAsync(new GetForumMessageQuery(id, UserId), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>الرسالة المثبّتة للشريط (`204` عند عدمها).</summary>
    [HttpGet("pinned")]
    public async Task<IActionResult> GetPinned(CancellationToken ct)
    {
        var dto = await _getPinned.HandleAsync(new GetPinnedForumMessageQuery(UserId), ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    /// <summary>نشر رسالة (مع اقتباس اختياري) — الهوية تُلقط خلفيًا لا من العميل.</summary>
    [HttpPost("messages")]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    public async Task<IActionResult> Post([FromBody] PostForumMessageRequest request, CancellationToken ct)
    {
        if (SubHeadSessionBroken)
            return Forbid();
        var message = await _postMessage.HandleAsync(
            new PostForumMessageCommand(request, UserId), ct);
        return CreatedAtAction(nameof(Get), new { id = message.Id }, message);
    }

    /// <summary>تعديل رسالة — الكاتب فقط (يختم «عُدّل»).</summary>
    [HttpPut("messages/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateForumMessageRequest request, CancellationToken ct)
    {
        if (SubHeadSessionBroken)
            return Forbid();
        try
        {
            return Ok(await _updateMessage.HandleAsync(
                new UpdateForumMessageCommand(id, request, UserId), ct));
        }
        catch (KeyNotFoundException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status404NotFound,
                            ApiErrorCodes.NotFound,
                            ApiErrorMessages.From(e));
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
            await _deleteMessage.HandleAsync(new DeleteForumMessageCommand(id, UserId), ct);
            return NoContent();
        }
        catch (KeyNotFoundException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status404NotFound,
                            ApiErrorCodes.NotFound,
                            ApiErrorMessages.From(e));
        }
    }

    /// <summary>تبديل التثبيت — المشرف فقط (الجديد يُسقط السابق).</summary>
    [HttpPost("messages/{id:int}/pin")]
    public async Task<IActionResult> TogglePin(int id, CancellationToken ct)
    {
        if (SubHeadSessionBroken)
            return Forbid();
        // التثبيت للمشرف حصرًا (مصفوفة §2-أ — المدير قارئ هنا لا مثبّت).
        if (!_capabilities.Has(User, UserCapability.IsAdmin))
            return Forbid();
        try
        {
            return Ok(await _togglePin.HandleAsync(
                new ToggleForumPinCommand(id, UserId), ct));
        }
        catch (KeyNotFoundException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status404NotFound,
                            ApiErrorCodes.NotFound,
                            ApiErrorMessages.From(e));
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

    /// <summary>تعليم القراءة حتى رسالة — عدّادي بلا تراجع.</summary>
    [HttpPost("read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkForumReadRequest request, CancellationToken ct)
    {
        if (SubHeadSessionBroken)
            return Forbid();
        var max = await _markRead.HandleAsync(
            new MarkForumReadCommand(UserId, ActorName, request.UpToMessageId), ct);
        return Ok(new { maxReadMessageId = max });
    }

    /// <summary>نافذة «شوهدت بواسطة» — الكاتب فقط.</summary>
    [HttpGet("messages/{id:int}/readers")]
    public async Task<IActionResult> GetReaders(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _getReaders.HandleAsync(
                new GetForumMessageReadersQuery(id, UserId), ct));
        }
        catch (KeyNotFoundException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status404NotFound,
                            ApiErrorCodes.NotFound,
                            ApiErrorMessages.From(e));
        }
    }

    /// <summary>مرشحو الذِكر بالاسم — نشطون فقط بلا الذات، بأجزاء الهوية.</summary>
    [HttpGet("users/lookup")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? q, CancellationToken ct)
    {
        return Ok(await _searchUsers.HandleAsync(new SearchForumUsersQuery(q, UserId), ct));
    }

    /// <summary>العدّاد — رسائل بلا إيصال لي (شارة أيقونة المنتدى).</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var count = await _countUnread.HandleAsync(new CountUnreadForumMessagesQuery(UserId), ct);
        return Ok(new { count });
    }
}
