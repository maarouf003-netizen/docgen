using DocGenerator.Api.Authorization;
using DocGenerator.Api.Http;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Correspondences;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// المراسلات (محامٍ/رئيس قسم أو شعبة/مندوب جهة): التسطير لطرف معيَّن بالاسم برقم وتاريخ
/// تلقائيين، واللاحق من المنشئ والرد من المستلم فقط، وتأكيد المشاهدة موثّق (من؟ متى؟).
/// كتابة المندوب تصل حصرًا عبر مسارات البوابة (PortalController) — هذا المتحكم
/// للمحامي ورئيس القسم والشعبة (كتابة وقراءة) والمدير/المشرف (قراءة بفلتر محافظة).
/// </summary>
[ApiController]
[Route("api/correspondence")]
[Authorize(Roles = "lawyer,head,subhead,manager,admin")]
public class CorrespondencesController : ControllerBase
{
    private readonly IUserCapabilities _capabilities;
    private readonly ICommandHandler<CreateCorrespondenceCommand, CorrespondenceDto> _create;
    private readonly ICommandHandler<AddCorrespondenceAddendumCommand, CorrespondenceMessageDto> _addAddendum;
    private readonly ICommandHandler<ReplyCorrespondenceCommand, CorrespondenceMessageDto> _reply;
    private readonly ICommandHandler<MarkCorrespondenceSeenCommand, CorrespondenceReceiptDto> _markSeen;
    private readonly IQueryHandler<SearchCorrespondencesQuery, PagedResult<CorrespondenceListItemDto>> _search;
    private readonly IQueryHandler<GetCorrespondenceQuery, CorrespondenceDto> _get;
    private readonly IQueryHandler<CountUnseenCorrespondencesQuery, int> _unseenCount;
    private readonly IQueryHandler<ListCorrespondencesByDocumentQuery, List<CorrespondenceListItemDto>> _listByDocument;
    private readonly IQueryHandler<GetCorrespondenceGovernoratesQuery, List<string>> _governorates;
    private readonly IQueryHandler<SearchCorrespondenceTargetsQuery, List<CorrespondenceTargetDto>> _targets;

    public CorrespondencesController(
        IUserCapabilities capabilities,
        ICommandHandler<CreateCorrespondenceCommand, CorrespondenceDto> create,
        ICommandHandler<AddCorrespondenceAddendumCommand, CorrespondenceMessageDto> addAddendum,
        ICommandHandler<ReplyCorrespondenceCommand, CorrespondenceMessageDto> reply,
        ICommandHandler<MarkCorrespondenceSeenCommand, CorrespondenceReceiptDto> markSeen,
        IQueryHandler<SearchCorrespondencesQuery, PagedResult<CorrespondenceListItemDto>> search,
        IQueryHandler<GetCorrespondenceQuery, CorrespondenceDto> get,
        IQueryHandler<CountUnseenCorrespondencesQuery, int> unseenCount,
        IQueryHandler<ListCorrespondencesByDocumentQuery, List<CorrespondenceListItemDto>> listByDocument,
        IQueryHandler<GetCorrespondenceGovernoratesQuery, List<string>> governorates,
        IQueryHandler<SearchCorrespondenceTargetsQuery, List<CorrespondenceTargetDto>> targets)
    {
        _capabilities = capabilities;
        _create = create;
        _addAddendum = addAddendum;
        _reply = reply;
        _markSeen = markSeen;
        _search = search;
        _get = get;
        _unseenCount = unseenCount;
        _listByDocument = listByDocument;
        _governorates = governorates;
        _targets = targets;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool IsSubHead => _capabilities.Has(User, UserCapability.IsSubHead);
    private bool IsEntityManager => _capabilities.Has(User, UserCapability.IsEntityManager);
    private bool IsLawyerOrHead =>
        _capabilities.Has(User, UserCapability.IsLawyer)
        || _capabilities.Has(User, UserCapability.IsHead);
    private int? BranchId => User.GetBranchId();
    private int? SectionId => User.GetSectionId();
    private int UserId => User.GetUserId();

    /// <summary>قائمة المراسلات بحسب الدور، مع بحث وفلتر محافظة/أهمية وترقيم.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] string? governorate, [FromQuery] string? importance,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        if (IsSubHead && SectionId is null)
            return Forbid();
        var result = await _search.HandleAsync(
            new SearchCorrespondencesQuery(UserId, Role, BranchId, q, governorate,
                importance, page, perPage, SectionId), ct);
        return Ok(result);
    }

    /// <summary>خيارات فلتر «المحافظة» المميزة — مدير/مشرف فقط.</summary>
    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanSeeAdministrativeBranch))
            return Ok(new { governorates = new List<string>() });

        var governorates = await _governorates.HandleAsync(new GetCorrespondenceGovernoratesQuery(), ct);
        return Ok(new { governorates });
    }

    /// <summary>مرشحو الاستلام بالاسم — محامٍ/رئيس قسم أو شعبة (المندوب عبر البوابة).</summary>
    [HttpGet("targets")]
    public async Task<IActionResult> SearchTargets(
        [FromQuery] string? q, [FromQuery] int? documentId, CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanCreateCorrespondences) || IsEntityManager)
            return Forbid();
        if (IsSubHead && SectionId is null)
            return Forbid();

        return Ok(await _targets.HandleAsync(
            new SearchCorrespondenceTargetsQuery(UserId, Role, BranchId, q, documentId, SectionId), ct));
    }

    /// <summary>عدد مراسلات المستخدم غير المؤكَّدة المشاهدة (بأي أهمية) — جرس المراسلات وشارتها.</summary>
    [HttpGet("unseen-count")]
    public async Task<IActionResult> UnseenCount(CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanCreateCorrespondences))
            return Forbid();

        var count = await _unseenCount.HandleAsync(new CountUnseenCorrespondencesQuery(UserId), ct);
        return Ok(new { count });
    }

    /// <summary>مراسلات ملف محدد — لمالك الملف ورئيس نطاقه والإدارة ومتابعيه.</summary>
    [HttpGet("document/{documentId:int}")]
    public async Task<IActionResult> ListByDocument(int documentId, CancellationToken ct)
    {
        try
        {
            var items = await _listByDocument.HandleAsync(
                new ListCorrespondencesByDocumentQuery(documentId, UserId, Role, BranchId, SectionId), ct);
            return Ok(items);
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

    /// <summary>تسطير مراسلة لطرف معيَّن — محامٍ/رئيس قسم أو شعبة (المندوب عبر البوابة).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCorrespondenceRequest request,
        CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanCreateCorrespondences) || IsEntityManager)
            return Forbid();
        if (IsLawyerOrHead && BranchId is null)
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.BranchRequired,
                "الحساب دون فرع لا يمكنه تسطير مراسلات");
        if (IsSubHead && SectionId is null)
            return Forbid();

        var letter = await _create.HandleAsync(
            new CreateCorrespondenceCommand(request, UserId, ActorName, Role, BranchId, SectionId), ct);
        return CreatedAtAction(nameof(Get), new { id = letter.Id }, letter);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        try
        {
            var letter = await _get.HandleAsync(
                new GetCorrespondenceQuery(id, UserId, Role, BranchId, SectionId), ct);
            return Ok(letter);
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

    /// <summary>إضافة لاحق إلى مراسلة — منشئ المراسلة نفسه فقط.</summary>
    [HttpPost("{id:int}/addenda")]
    public async Task<IActionResult> AddAddendum(int id,
        [FromBody] AddCorrespondenceAddendumRequest request, CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanCreateCorrespondences) || IsEntityManager)
            return Forbid();

        try
        {
            var addendum = await _addAddendum.HandleAsync(
                new AddCorrespondenceAddendumCommand(id, request, UserId, ActorName), ct);
            return Ok(addendum);
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

    /// <summary>رد الطرف المستلم على المراسلة — المستلم المعيَّن فقط.</summary>
    [HttpPost("{id:int}/replies")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyCorrespondenceRequest request,
        CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanCreateCorrespondences) || IsEntityManager)
            return Forbid();

        try
        {
            var reply = await _reply.HandleAsync(
                new ReplyCorrespondenceCommand(id, request, UserId, ActorName), ct);
            return Ok(reply);
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

    /// <summary>تأكيد المشاهدة الصريح — الطرف المستلم وحده (يفرضه الخادم بـ403 لغيره).</summary>
    [HttpPost("{id:int}/mark-seen")]
    public async Task<IActionResult> MarkSeen(int id, CancellationToken ct)
    {
        // بوابة الدور الصريحة نفسها في كل مسارات الكتابة: الخدمة تفرض المستلم،
        // وهذه البوابة تمنع أي دور غير كاتب من المحاولة أصلًا (دفاع معمّق).
        if (!_capabilities.Has(User, UserCapability.CanCreateCorrespondences) || IsEntityManager)
            return Forbid();

        try
        {
            var receipt = await _markSeen.HandleAsync(
                new MarkCorrespondenceSeenCommand(id, UserId, ActorName, Role, BranchId, SectionId), ct);
            return Ok(receipt);
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
}
