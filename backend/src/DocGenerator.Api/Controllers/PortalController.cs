using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly ICorrespondenceService _correspondence;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public PortalController(IPortalService portal, ICorrespondenceService correspondence, TimeProvider clock, TimeZoneInfo timeZone)
    {
        _portal = portal;
        _correspondence = correspondence;
        _clock = clock;
        _timeZone = timeZone;
    }

    private string? ViewerName => User.Identity?.Name;
    private int UserId => User.GetUserId();

    /// <summary>ما يُسمح للمندوب برؤيته: الهوية/الفرع المربوط بحسابه وفروعه النهائية.</summary>
    [HttpGet("my-scope")]
    public async Task<IActionResult> MyScope(CancellationToken ct)
        => Ok(await _portal.GetMyScopeAsync(UserId, ct));

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
        try
        {
            return Ok(await _portal.ListFilesAsync(UserId, q, status, page, perPage, ct, entryId));
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
        var appeals = await _portal.ListAppealsAsync(UserId, id, ct);
        return appeals is null ? NotFound() : Ok(appeals);
    }

    /// <summary>الإجراءات التنفيذية للملف (نوع action فقط) — 404 عند الخروج عن النطاق.</summary>
    [HttpGet("files/{id:int}/execution-actions")]
    public async Task<IActionResult> ExecutionActions(int id, CancellationToken ct)
    {
        var actions = await _portal.ListExecutionActionsAsync(UserId, id, ct);
        return actions is null ? NotFound() : Ok(actions);
    }

    /// <summary>تشعبات الملف القرائية (إنابة) — 404 عند الخروج عن النطاق.</summary>
    [HttpGet("files/{id:int}/delegations")]
    public async Task<IActionResult> Delegations(int id, CancellationToken ct)
    {
        var delegations = await _portal.ListDelegationsAsync(UserId, id, ct);
        return delegations is null ? NotFound() : Ok(delegations);
    }

    /// <summary>تفاصيل استئنافات الملف (رأي المحامي مخفي) — 404 عند الخروج عن النطاق.</summary>
    [HttpGet("files/{id:int}/appeals/details")]
    public async Task<IActionResult> AppealDetails(int id, CancellationToken ct)
    {
        var appeals = await _portal.ListAppealDetailsAsync(UserId, id, ct);
        return appeals is null ? NotFound() : Ok(appeals);
    }

    /// <summary>تاريخ أرقام الأساس للملف — 404 عند الخروج عن النطاق.</summary>
    [HttpGet("files/{id:int}/base-numbers")]
    public async Task<IActionResult> BaseNumbers(int id, CancellationToken ct)
    {
        var history = await _portal.ListBaseNumbersAsync(UserId, id, ct);
        return history is null ? NotFound() : Ok(history);
    }

    /// <summary>إحصاءات قرائية لنطاق الجهة (المرحلة 4) — إجمالي أو فرع مختار ضمن النطاق.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> Stats([FromQuery] int? entryId = null, CancellationToken ct = default)
    {
        try
        {
            return Ok(await _portal.GetStatsAsync(UserId, ct, entryId));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>تصدير Excel لملفات النطاق وفق نفس الفلاتر وبسقف صفوف التصدير.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] int? entryId = null,
        CancellationToken ct = default)
    {
        try
        {
            var bytes = await _portal.ExportWorkbookAsync(UserId, q, status, ViewerName, ct, entryId);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"الملفات التنفيذية {ServerClock.TodayString(_clock, _timeZone, "yyyy-MM-dd")}.xlsx");
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
        try
        {
            return Ok(await _correspondence.SearchAsync(UserId,
                DocGenerator.Domain.Enums.UserRole.EntityManager, null,
                q, null, importance, page, perPage, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>مرشحو الاستلام بالاسم لمندوب الجهة — مع documentId محامو الملف فقط.</summary>
    [HttpGet("correspondence/targets")]
    public async Task<IActionResult> CorrespondenceTargets(
        [FromQuery] string? q, [FromQuery] int? documentId, CancellationToken ct)
    {
        try
        {
            return Ok(await _correspondence.SearchTargetsAsync(UserId,
                DocGenerator.Domain.Enums.UserRole.EntityManager, null, q, documentId, ct));
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

    /// <summary>عدد مراسلات المندوب العاجلة بلا تأكيد مشاهدة — جرس البوابة.</summary>
    [HttpGet("correspondence/urgent-unseen-count")]
    public async Task<IActionResult> CorrespondenceUrgentCount(CancellationToken ct)
        => Ok(new { count = await _correspondence.CountUrgentUnseenAsync(UserId, ct) });

    /// <summary>مراسلة المندوب برسائلها — مقصورة على ما هو طرف فيه.</summary>
    [HttpGet("correspondence/{id:int}")]
    public async Task<IActionResult> CorrespondenceGet(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _correspondence.GetByIdAsync(id, UserId,
                DocGenerator.Domain.Enums.UserRole.EntityManager, null, ct));
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

    /// <summary>تسطير مراسلة من المندوب لطرف معيَّن بالاسم.</summary>
    [HttpPost("correspondence")]
    public async Task<IActionResult> CorrespondenceCreate(
        [FromBody] CreateCorrespondenceRequest request, CancellationToken ct)
    {
        try
        {
            var letter = await _correspondence.CreateAsync(request, UserId, ViewerName,
                DocGenerator.Domain.Enums.UserRole.EntityManager, null, ct);
            return CreatedAtAction(nameof(CorrespondenceGet), new { id = letter.Id }, letter);
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

    /// <summary>إضافة لاحق — مندوب منشئ المراسلة نفسه فقط.</summary>
    [HttpPost("correspondence/{id:int}/addenda")]
    public async Task<IActionResult> CorrespondenceAddAddendum(int id,
        [FromBody] AddCorrespondenceAddendumRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _correspondence.AddAddendumAsync(id, request, UserId, ViewerName, ct));
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

    /// <summary>رد المندوب المستلم على المراسلة — المستلم المعيَّن فقط.</summary>
    [HttpPost("correspondence/{id:int}/replies")]
    public async Task<IActionResult> CorrespondenceReply(int id,
        [FromBody] ReplyCorrespondenceRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _correspondence.ReplyAsync(id, request, UserId, ViewerName, ct));
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

    /// <summary>تأكيد مشاهدة المندوب — زر «تمت المشاهدة».</summary>
    [HttpPost("correspondence/{id:int}/mark-seen")]
    public async Task<IActionResult> CorrespondenceMarkSeen(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _correspondence.MarkSeenAsync(id, UserId, ViewerName,
                DocGenerator.Domain.Enums.UserRole.EntityManager, null, ct));
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

    /// <summary>مراسلات ملف من نطاق المندوب — ما هو طرف فيه فقط، 404 خارج النطاق.</summary>
    [HttpGet("files/{id:int}/correspondence")]
    public async Task<IActionResult> FileCorrespondence(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _correspondence.ListByDocumentAsync(id, UserId,
                DocGenerator.Domain.Enums.UserRole.EntityManager, null, ct));
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }
}

/// <summary>إدارة حسابات مندوبي الجهات وربط نطاقهم — مدير/مشرف/رئيس قسم (د11).</summary>
[ApiController]
[Route("api/entity-portal/delegates")]
[Authorize]
public class DelegatesController : ControllerBase
{
    private readonly IEntityDelegateService _delegates;

    public DelegatesController(IEntityDelegateService delegates) => _delegates = delegates;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!RolePermissions.CanManageDelegates(User.GetRoleEnum()))
            return Forbid();
        return Ok(await _delegates.ListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDelegateRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageDelegates(User.GetRoleEnum()))
            return Forbid();
        try
        {
            return Ok(await _delegates.CreateAsync(request, User.Identity?.Name, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDelegateRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageDelegates(User.GetRoleEnum()))
            return Forbid();
        try
        {
            var dto = await _delegates.UpdateAsync(id, request, User.Identity?.Name, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }
}
