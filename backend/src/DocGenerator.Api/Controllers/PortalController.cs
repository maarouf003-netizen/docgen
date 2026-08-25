using DocGenerator.Api.Authorization;
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

    public PortalController(IPortalService portal) => _portal = portal;

    private string? ViewerName => User.Identity?.Name;
    private int UserId => User.GetUserId();

    /// <summary>ما يُسمح للمندوب برؤيته: الهوية/القيد المربوط بحسابه وقيوده النهائية.</summary>
    [HttpGet("my-scope")]
    public async Task<IActionResult> MyScope(CancellationToken ct)
        => Ok(await _portal.GetMyScopeAsync(UserId, ct));

    /// <summary>قائمة ملفات الجهة (قراءة فقط) بنفس فلاتر القائمة الأساسية.</summary>
    [HttpGet("files")]
    public async Task<IActionResult> Files(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20,
        CancellationToken ct = default)
        => Ok(await _portal.ListFilesAsync(UserId, q, status, page, perPage, ct));

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

    /// <summary>إحصاءات قرائية لنطاق الجهة (المرحلة 4).</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
        => Ok(await _portal.GetStatsAsync(UserId, ct));

    /// <summary>تصدير Excel لملفات النطاق وفق نفس الفلاتر وبسقف صفوف التصدير.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? q,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        try
        {
            var bytes = await _portal.ExportWorkbookAsync(UserId, q, status, ViewerName, ct);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ملفات الجهة {DateTime.Now:yyyy-MM-dd}.xlsx");
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
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
