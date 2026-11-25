using DocGenerator.Api.Authorization;
using DocGenerator.Api.Http;
using DocGenerator.Api.Security;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Correspondences;
using DocGenerator.Application.UseCases.Portal;
using DocGenerator.Application.UseCases.PublicEntities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// بوابة مندوب الجهة العامة (المرحلة 3): مسارات قرائية حصرية لعزل بنيوي — لا
/// توجد هنا أي نقطة إنشاء/تعديل/حالة/توليد، والنطاق مفروض في الخدمة لكل طلب
/// بحيث يُترجم الخروج عنه إلى 404 دون كشف وجود الملف.
/// </summary>
[ApiController]
[Route("api/portal")]
[Authorize(Roles = "entitymanager")]
public class PortalController : ControllerBase
{
    private readonly IPortalService _portal;
    private readonly ICommandHandler<CreateCorrespondenceCommand, CorrespondenceDto> _createCorrespondence;
    private readonly ICommandHandler<AddCorrespondenceAddendumCommand, CorrespondenceMessageDto> _addCorrespondenceAddendum;
    private readonly ICommandHandler<ReplyCorrespondenceCommand, CorrespondenceMessageDto> _replyCorrespondence;
    private readonly ICommandHandler<MarkCorrespondenceSeenCommand, CorrespondenceReceiptDto> _markCorrespondenceSeen;
    private readonly IQueryHandler<SearchCorrespondencesQuery, PagedResult<CorrespondenceListItemDto>> _searchCorrespondence;
    private readonly IQueryHandler<SearchCorrespondenceTargetsQuery, List<CorrespondenceTargetDto>> _correspondenceTargets;
    private readonly IQueryHandler<CountUnseenCorrespondencesQuery, int> _correspondenceUnseenCount;
    private readonly IQueryHandler<GetCorrespondenceQuery, CorrespondenceDto> _getCorrespondence;
    private readonly IQueryHandler<ListCorrespondencesByDocumentQuery, List<CorrespondenceListItemDto>> _fileCorrespondence;
    private readonly IQueryHandler<GetPortalScopeQuery, PortalScopeDto?> _scope;
    private readonly IQueryHandler<ListPortalFilesQuery, PagedResult<PortalFileListItemDto>> _files;
    private readonly IQueryHandler<ListPortalAppealsQuery, IReadOnlyList<PortalAppealDto>?> _portalAppeals;
    private readonly IQueryHandler<ListPortalExecutionActionsQuery, IReadOnlyList<PortalExecutionActionDto>?> _executionActions;
    private readonly IQueryHandler<ListPortalDelegationsQuery, IReadOnlyList<DelegationDto>?> _portalDelegations;
    private readonly IQueryHandler<ListPortalAppealDetailsQuery, IReadOnlyList<AppealDto>?> _appealDetails;
    private readonly IQueryHandler<ListPortalBaseNumbersQuery, IReadOnlyList<BaseNumberHistoryDto>?> _baseNumbers;
    private readonly IQueryHandler<GetPortalStatsQuery, PortalStatsDto> _portalStats;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public PortalController(
        IPortalService portal,
        ICommandHandler<CreateCorrespondenceCommand, CorrespondenceDto> createCorrespondence,
        ICommandHandler<AddCorrespondenceAddendumCommand, CorrespondenceMessageDto> addCorrespondenceAddendum,
        ICommandHandler<ReplyCorrespondenceCommand, CorrespondenceMessageDto> replyCorrespondence,
        ICommandHandler<MarkCorrespondenceSeenCommand, CorrespondenceReceiptDto> markCorrespondenceSeen,
        IQueryHandler<SearchCorrespondencesQuery, PagedResult<CorrespondenceListItemDto>> searchCorrespondence,
        IQueryHandler<SearchCorrespondenceTargetsQuery, List<CorrespondenceTargetDto>> correspondenceTargets,
        IQueryHandler<CountUnseenCorrespondencesQuery, int> correspondenceUnseenCount,
        IQueryHandler<GetCorrespondenceQuery, CorrespondenceDto> getCorrespondence,
        IQueryHandler<ListCorrespondencesByDocumentQuery, List<CorrespondenceListItemDto>> fileCorrespondence,
        IQueryHandler<GetPortalScopeQuery, PortalScopeDto?> scope,
        IQueryHandler<ListPortalFilesQuery, PagedResult<PortalFileListItemDto>> files,
        IQueryHandler<ListPortalAppealsQuery, IReadOnlyList<PortalAppealDto>?> portalAppeals,
        IQueryHandler<ListPortalExecutionActionsQuery, IReadOnlyList<PortalExecutionActionDto>?> executionActions,
        IQueryHandler<ListPortalDelegationsQuery, IReadOnlyList<DelegationDto>?> portalDelegations,
        IQueryHandler<ListPortalAppealDetailsQuery, IReadOnlyList<AppealDto>?> appealDetails,
        IQueryHandler<ListPortalBaseNumbersQuery, IReadOnlyList<BaseNumberHistoryDto>?> baseNumbers,
        IQueryHandler<GetPortalStatsQuery, PortalStatsDto> portalStats,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _portal = portal;
        _createCorrespondence = createCorrespondence;
        _addCorrespondenceAddendum = addCorrespondenceAddendum;
        _replyCorrespondence = replyCorrespondence;
        _markCorrespondenceSeen = markCorrespondenceSeen;
        _searchCorrespondence = searchCorrespondence;
        _correspondenceTargets = correspondenceTargets;
        _correspondenceUnseenCount = correspondenceUnseenCount;
        _getCorrespondence = getCorrespondence;
        _fileCorrespondence = fileCorrespondence;
        _scope = scope;
        _files = files;
        _portalAppeals = portalAppeals;
        _executionActions = executionActions;
        _portalDelegations = portalDelegations;
        _appealDetails = appealDetails;
        _baseNumbers = baseNumbers;
        _portalStats = portalStats;
        _clock = clock;
        _timeZone = timeZone;
    }

    private string? ViewerName => User.Identity?.Name;
    private int UserId => User.GetUserId();

    /// <summary>ما يُسمح للمندوب برؤيته: الهوية/الفرع المربوط بحسابه وفروعه النهائية.</summary>
    [HttpGet("my-scope")]
    public async Task<IActionResult> MyScope(CancellationToken ct)
        => Ok(await _scope.HandleAsync(new GetPortalScopeQuery(UserId), ct));

    /// <summary>قائمة ملفات الجهة (قراءة فقط) بنفس فلاتر القائمة الأساسية + فلتر فرع ضمن النطاق.</summary>
    [HttpGet("files")]
    public async Task<IActionResult> Files(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20,
        [FromQuery] int? entryId = null,
        CancellationToken ct = default)
    {
        return Ok(await _files.HandleAsync(
            new ListPortalFilesQuery(UserId, q, status, page, perPage, entryId), ct));
    }

    /// <summary>تفاصيل ملف قراءةً — 404 عند الخروج عن النطاق دون كشف الوجود.</summary>
    [HttpGet("files/{id:int}")]
    public async Task<IActionResult> File(int id, CancellationToken ct)
    {
        var file = await _portal.GetFileAsync(UserId, id, ViewerName, ct);
        return file is null ? NotFound() : Ok(file);
    }

    /// <summary>بطاقة الاستئنافات القرائية للملف — 404 عند الخروج عن النطاق.</summary>
    [HttpGet("files/{id:int}/appeals")]
    public async Task<IActionResult> Appeals(int id, CancellationToken ct)
    {
        var appeals = await _portalAppeals.HandleAsync(new ListPortalAppealsQuery(UserId, id), ct);
        return appeals is null ? NotFound() : Ok(appeals);
    }

    /// <summary>الإجراءات التنفيذية للملف (نوع action فقط) — 404 عند الخروج عن النطاق.</summary>
    [HttpGet("files/{id:int}/execution-actions")]
    public async Task<IActionResult> ExecutionActions(int id, CancellationToken ct)
    {
        var actions = await _executionActions.HandleAsync(new ListPortalExecutionActionsQuery(UserId, id), ct);
        return actions is null ? NotFound() : Ok(actions);
    }

    /// <summary>تشعبات الملف القرائية (إنابة) — 404 عند الخروج عن النطاق.</summary>
    [HttpGet("files/{id:int}/delegations")]
    public async Task<IActionResult> Delegations(int id, CancellationToken ct)
    {
        var delegations = await _portalDelegations.HandleAsync(new ListPortalDelegationsQuery(UserId, id), ct);
        return delegations is null ? NotFound() : Ok(delegations);
    }

    /// <summary>تفاصيل استئنافات الملف (رأي المحامي مخفي) — 404 عند الخروج عن النطاق.</summary>
    [HttpGet("files/{id:int}/appeals/details")]
    public async Task<IActionResult> AppealDetails(int id, CancellationToken ct)
    {
        var appeals = await _appealDetails.HandleAsync(new ListPortalAppealDetailsQuery(UserId, id), ct);
        return appeals is null ? NotFound() : Ok(appeals);
    }

    /// <summary>تاريخ أرقام الأساس للملف — 404 عند الخروج عن النطاق.</summary>
    [HttpGet("files/{id:int}/base-numbers")]
    public async Task<IActionResult> BaseNumbers(int id, CancellationToken ct)
    {
        var history = await _baseNumbers.HandleAsync(new ListPortalBaseNumbersQuery(UserId, id), ct);
        return history is null ? NotFound() : Ok(history);
    }

    /// <summary>إحصاءات قرائية لنطاق الجهة (المرحلة 4) — إجمالي أو فرع مختار ضمن النطاق.</summary>
    [HttpGet("stats")]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    public async Task<IActionResult> Stats([FromQuery] int? entryId = null, CancellationToken ct = default)
    {
        return Ok(await _portalStats.HandleAsync(new GetPortalStatsQuery(UserId, entryId), ct));
    }

    /// <summary>تصدير Excel لملفات النطاق وفق نفس الفلاتر وبسقف صفوف التصدير.</summary>
    [HttpGet("export")]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    [SingleExportPerUser]
    public async Task<IActionResult> Export(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] int? entryId = null,
        CancellationToken ct = default)
    {
        var stream = await _portal.ExportWorkbookAsync(UserId, q, status, ViewerName, ct, entryId);
        return File(stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"الملفات التنفيذية {ServerClock.TodayString(_clock, _timeZone, "yyyy-MM-dd")}.xlsx");
    }

    // ── مراسلات المندوب: الاستثناء الكتابي الوحيد في البوابة القرائية ──
    // المندوب طرف كامل (تسطير/لاحق/رد/مشاهدة) لكن حصرًا عبر هذه المسارات المقيدة
    // بنطاقه وبدور EntityManager، دون المساس بحارس العزل البنيوي.

    /// <summary>مراسلات المندوب (كطرف منشئ أو مستلم) مع البحث وفلتر الأهمية.</summary>
    [HttpGet("correspondence")]
    public async Task<IActionResult> CorrespondenceList(
        [FromQuery] string? q,
        [FromQuery] string? importance,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        return Ok(await _searchCorrespondence.HandleAsync(
            new SearchCorrespondencesQuery(UserId,
                DocGenerator.Domain.Enums.UserRole.EntityManager, null,
                q, null, importance, page, perPage), ct));
    }

    /// <summary>مرشحو الاستلام بالاسم لمندوب الجهة — مع documentId محامو الملف فقط.</summary>
    [HttpGet("correspondence/targets")]
    public async Task<IActionResult> CorrespondenceTargets(
        [FromQuery] string? q, [FromQuery] int? documentId, CancellationToken ct)
    {
        return Ok(await _correspondenceTargets.HandleAsync(
            new SearchCorrespondenceTargetsQuery(UserId,
                DocGenerator.Domain.Enums.UserRole.EntityManager, null, q, documentId), ct));
    }

    /// <summary>عدد مراسلات المندوب غير المؤكَّدة المشاهدة (بأي أهمية) — جرس البوابة وشارتها.</summary>
    [HttpGet("correspondence/unseen-count")]
    public async Task<IActionResult> CorrespondenceUnseenCount(CancellationToken ct)
    {
        var count = await _correspondenceUnseenCount.HandleAsync(
            new CountUnseenCorrespondencesQuery(UserId), ct);
        return Ok(new { count });
    }

    /// <summary>مراسلة المندوب برسائلها — مقصورة على ما هو طرف فيه.</summary>
    [HttpGet("correspondence/{id:int}")]
    public async Task<IActionResult> CorrespondenceGet(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _getCorrespondence.HandleAsync(
                new GetCorrespondenceQuery(id, UserId,
                    DocGenerator.Domain.Enums.UserRole.EntityManager, null), ct));
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

    /// <summary>تسطير مراسلة من المندوب لطرف معيَّن بالاسم.</summary>
    [HttpPost("correspondence")]
    public async Task<IActionResult> CorrespondenceCreate(
        [FromBody] CreateCorrespondenceRequest request, CancellationToken ct)
    {
        var letter = await _createCorrespondence.HandleAsync(
            new CreateCorrespondenceCommand(request, UserId, ViewerName,
                DocGenerator.Domain.Enums.UserRole.EntityManager, null), ct);
        return CreatedAtAction(nameof(CorrespondenceGet), new { id = letter.Id }, letter);
    }

    /// <summary>إضافة لاحق — مندوب منشئ المراسلة نفسه فقط.</summary>
    [HttpPost("correspondence/{id:int}/addenda")]
    public async Task<IActionResult> CorrespondenceAddAddendum(int id,
        [FromBody] AddCorrespondenceAddendumRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _addCorrespondenceAddendum.HandleAsync(
                new AddCorrespondenceAddendumCommand(id, request, UserId, ViewerName), ct));
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

    /// <summary>رد المندوب المستلم على المراسلة — المستلم المعيَّن فقط.</summary>
    [HttpPost("correspondence/{id:int}/replies")]
    public async Task<IActionResult> CorrespondenceReply(int id,
        [FromBody] ReplyCorrespondenceRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _replyCorrespondence.HandleAsync(
                new ReplyCorrespondenceCommand(id, request, UserId, ViewerName), ct));
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

    /// <summary>تأكيد مشاهدة المندوب — زر «تمت المشاهدة».</summary>
    [HttpPost("correspondence/{id:int}/mark-seen")]
    public async Task<IActionResult> CorrespondenceMarkSeen(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _markCorrespondenceSeen.HandleAsync(
                new MarkCorrespondenceSeenCommand(id, UserId, ViewerName,
                    DocGenerator.Domain.Enums.UserRole.EntityManager, null), ct));
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

    /// <summary>مراسلات ملف من نطاق المندوب — ما هو طرف فيه فقط، 404 خارج النطاق.</summary>
    [HttpGet("files/{id:int}/correspondence")]
    public async Task<IActionResult> FileCorrespondence(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _fileCorrespondence.HandleAsync(
                new ListCorrespondencesByDocumentQuery(id, UserId,
                    DocGenerator.Domain.Enums.UserRole.EntityManager, null), ct));
        }
        catch (KeyNotFoundException)
        {
            // إخفاء مقصود: لا تمييز بين «ملف غير موجود» و«خارج النطاق».
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }
}

/// <summary>إدارة حسابات مندوبي الجهات وربط نطاقهم — مدير/مشرف/رئيس قسم وشعبة (د11).</summary>
[ApiController]
[Route("api/entity-portal/delegates")]
[Authorize(Roles = "manager,admin,head,subhead")]
public class DelegatesController : ControllerBase
{
    private readonly IEntityDelegateService _delegates;
    private readonly IUserCapabilities _capabilities;
    private readonly ICommandHandler<CreateEntityDelegateCommand, DelegateDto> _createDelegate;
    private readonly ICommandHandler<UpdateEntityDelegateCommand, DelegateDto?> _updateDelegate;

    public DelegatesController(
        IEntityDelegateService delegates,
        IUserCapabilities capabilities,
        ICommandHandler<CreateEntityDelegateCommand, DelegateDto> createDelegate,
        ICommandHandler<UpdateEntityDelegateCommand, DelegateDto?> updateDelegate)
    {
        _delegates = delegates;
        _capabilities = capabilities;
        _createDelegate = createDelegate;
        _updateDelegate = updateDelegate;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanManageDelegates))
            return Forbid();
        return Ok(await _delegates.ListAsync(ct, Actor()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDelegateRequest request, CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanManageDelegates))
            return Forbid();
        return Ok(await _createDelegate.HandleAsync(
            new CreateEntityDelegateCommand(request, User.Identity?.Name, Actor()), ct));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDelegateRequest request, CancellationToken ct)
    {
        if (!_capabilities.Has(User, UserCapability.CanManageDelegates))
            return Forbid();
        var dto = await _updateDelegate.HandleAsync(
            new UpdateEntityDelegateCommand(id, request, User.Identity?.Name, Actor()), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    private DocGenerator.Application.Services.EntityRegistryActor Actor() =>
        new(User.GetUserId(), User.Identity?.Name, User.GetRoleEnum(), User.GetBranchId());
}
