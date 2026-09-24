using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// المراسلات (محامٍ/رئيس قسم/مندوب جهة): التسطير لطرف معيَّن بالاسم برقم وتاريخ
/// تلقائيين، واللاحق من المنشئ والرد من المستلم فقط، وتأكيد المشاهدة موثّق (من؟ متى؟).
/// كتابة المندوب تصل حصرًا عبر مسارات البوابة (PortalController) — هذا المتحكم
/// للمحامي ورئيس القسم (كتابة وقراءة) والمدير/المشرف (قراءة بفلتر محافظة).
/// </summary>
[ApiController]
[Route("api/correspondence")]
[Authorize]
public class CorrespondencesController : ControllerBase
{
    private readonly ICorrespondenceService _letters;

    public CorrespondencesController(ICorrespondenceService letters)
    {
        _letters = letters;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private int? BranchId => User.GetBranchId();
    private int UserId => User.GetUserId();

    /// <summary>قائمة المراسلات بحسب الدور، مع بحث وفلتر محافظة/أهمية وترقيم.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] string? governorate, [FromQuery] string? importance,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _letters.SearchAsync(UserId, Role, BranchId, q, governorate,
                importance, page, perPage, ct);
            return Ok(result);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>خيارات فلتر «المحافظة» المميزة — مدير/مشرف فقط.</summary>
    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(CancellationToken ct)
    {
        if (!RolePermissions.CanSeeAdministrativeBranch(Role))
            return Ok(new { governorates = new List<string>() });

        var governorates = await _letters.GetGovernoratesAsync(ct);
        return Ok(new { governorates });
    }

    /// <summary>مرشحو الاستلام بالاسم — محامٍ/رئيس قسم (المندوب عبر البوابة).</summary>
    [HttpGet("targets")]
    public async Task<IActionResult> SearchTargets([FromQuery] string? q, CancellationToken ct)
    {
        if (!RolePermissions.CanCreateCorrespondences(Role) || Role == UserRole.EntityManager)
            return Forbid();

        try
        {
            return Ok(await _letters.SearchTargetsAsync(UserId, Role, q, ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>عدد مراسلات المستخدم العاجلة بلا تأكيد مشاهدة — جرس المراسلات.</summary>
    [HttpGet("urgent-unseen-count")]
    public async Task<IActionResult> UrgentUnseenCount(CancellationToken ct)
    {
        if (!RolePermissions.CanCreateCorrespondences(Role))
            return Forbid();

        var count = await _letters.CountUrgentUnseenAsync(UserId, ct);
        return Ok(new { count });
    }

    /// <summary>مراسلات ملف محدد — لمالك الملف ورئيس قسمه والإدارة ومتابعيه.</summary>
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

    /// <summary>تسطير مراسلة لطرف معيَّن — محامٍ/رئيس قسم (المندوب عبر البوابة).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCorrespondenceRequest request,
        CancellationToken ct)
    {
        if (!RolePermissions.CanCreateCorrespondences(Role) || Role == UserRole.EntityManager)
            return Forbid();
        if (Role is UserRole.Lawyer or UserRole.Head && BranchId is null)
            return BadRequest(new { message = "الحساب دون فرع لا يمكنه تسطير مراسلات" });

        try
        {
            var letter = await _letters.CreateAsync(request, UserId, ActorName, Role, BranchId, ct);
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

    /// <summary>إضافة لاحق إلى مراسلة — منشئ المراسلة نفسه فقط.</summary>
    [HttpPost("{id:int}/addenda")]
    public async Task<IActionResult> AddAddendum(int id,
        [FromBody] AddCorrespondenceAddendumRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanCreateCorrespondences(Role) || Role == UserRole.EntityManager)
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

    /// <summary>رد الطرف المستلم على المراسلة — المستلم المعيَّن فقط.</summary>
    [HttpPost("{id:int}/replies")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyCorrespondenceRequest request,
        CancellationToken ct)
    {
        if (!RolePermissions.CanCreateCorrespondences(Role) || Role == UserRole.EntityManager)
            return Forbid();

        try
        {
            var reply = await _letters.ReplyAsync(id, request, UserId, ActorName, ct);
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

    /// <summary>تأكيد المشاهدة الصريح — زر «تمت المشاهدة» لأي مطّلع مخوّل.</summary>
    [HttpPost("{id:int}/mark-seen")]
    public async Task<IActionResult> MarkSeen(int id, CancellationToken ct)
    {
        try
        {
            var receipt = await _letters.MarkSeenAsync(id, UserId, ActorName, Role, BranchId, ct);
            return Ok(receipt);
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
