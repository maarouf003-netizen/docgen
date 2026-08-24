using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// السجل المرجعي للجهات العامة (المرحلة 1): إدارة القيود وهوياتها للأدوار
/// المخوّلة عبر RolePermissions، اقتراحات المحامين واعتمادها من رئيس القسم،
/// وأداة الاستيراد التاريخي الإدارية. حدود محافظة رئيس القسم مفروضة في الخدمة.
/// </summary>
[ApiController]
[Route("api/entity-registry")]
[Authorize]
public class EntityRegistryController : ControllerBase
{
    private readonly IPublicEntityService _registry;

    public EntityRegistryController(IPublicEntityService registry) => _registry = registry;

    private string? ActorName => User.Identity?.Name;
    private UserRole Role => User.GetRoleEnum();
    private EntityRegistryActor Actor => new(User.GetUserId(), ActorName, Role, User.GetBranchId());

    /// <summary>قائمة السجل لشاشة الإدارة — رئيس قسم/مدير/مشرف (تشمل قيود الانتظار).</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] string? governorate,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        return Ok(await _registry.ListAsync(
            new EntityRegistryListQuery(q, governorate, status, IncludePending: true, page, perPage), ct));
    }

    /// <summary>بحث السجل (لنافذة الإدخال لاحقًا) — متاح لكل الطاقم عدا دور المندوب، وقيد الانتظار لا يظهر إلا للمخوّلين.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? governorate,
        CancellationToken ct = default)
    {
        if (Role == UserRole.EntityManager)
            return Forbid();
        var includePending = RolePermissions.CanManageEntityRegistry(Role);
        return Ok(await _registry.ListAsync(
            new EntityRegistryListQuery(q, governorate, null, includePending, 1, 50), ct));
    }

    /// <summary>إنشاء قيد نهائي مباشر — رئيس قسم (ضمن محافظته)/مدير/مشرف.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePublicEntityRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.CreateAsync(request, Actor, ct));
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

    /// <summary>تعديل قيد/إعادة تسمية جماعية بمزامنة النصوص وتدقيق الحقول — نطاقات د5/د6.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePublicEntityRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            var updated = await _registry.UpdateAsync(id, request, Actor, ct);
            return updated is null ? NotFound() : Ok(updated);
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

    /// <summary>إضافة اسم كتابي بديل لقيد.</summary>
    [HttpPost("{id:int}/aliases")]
    public async Task<IActionResult> AddAlias(int id, [FromBody] AddPublicEntityAliasRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            var updated = await _registry.AddAliasAsync(id, request, Actor, ct);
            return updated is null ? NotFound() : Ok(updated);
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

    // ── الاقتراحات ──

    /// <summary>تقديم اقتراح جهة جديدة — المحامي فقط (د4)، ويدخل بحالة انتظار.</summary>
    [HttpPost("proposals")]
    [Authorize(Roles = "lawyer")]
    public async Task<IActionResult> CreateProposal([FromBody] CreatePublicEntityProposalRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _registry.CreateProposalAsync(request, User.GetUserId(), ActorName, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>نافذة انتظار الاعتماد — رئيس القسم ضمن محافظته (د4).</summary>
    [HttpGet("proposals/pending")]
    public async Task<IActionResult> PendingProposals(CancellationToken ct)
    {
        if (!RolePermissions.CanApproveEntityProposals(Role) && !RolePermissions.HasFullAccess(Role))
            return Forbid();
        return Ok(await _registry.ListPendingProposalsAsync(Actor, ct));
    }

    /// <summary>اعتماد اقتراح وإنشاء القيد النهائي — رئيس القسم ضمن محافظته.</summary>
    [HttpPost("proposals/{id:int}/approve")]
    public async Task<IActionResult> ApproveProposal(int id, CancellationToken ct)
    {
        if (!RolePermissions.CanApproveEntityProposals(Role))
            return Forbid();
        try
        {
            var dto = await _registry.ApproveProposalAsync(id, Actor, ct);
            return dto is null ? NotFound() : Ok(dto);
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

    /// <summary>رفض اقتراح بسبب معلن — رئيس القسم ضمن محافظته.</summary>
    [HttpPost("proposals/{id:int}/reject")]
    public async Task<IActionResult> RejectProposal(int id, [FromBody] RejectPublicEntityProposalRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanApproveEntityProposals(Role))
            return Forbid();
        try
        {
            var dto = await _registry.RejectProposalAsync(id, request, Actor, ct);
            return dto is null ? NotFound() : Ok(dto);
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

    // ── الاستيراد التاريخي (د12) — إداري: مدير/مشرف ──

    /// <summary>معاينة النصوص التاريخية المتمايزة بعد التطبيع مع عدّادات ملفاتها.</summary>
    [HttpPost("import-preview")]
    public async Task<IActionResult> ImportPreview(CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        return Ok(await _registry.PreviewImportAsync(ct));
    }

    /// <summary>اعتماد الربط الجماعي: إنشاء هويات وقيود نهائية Final مباشرة مع الأسماء البديلة.</summary>
    [HttpPost("import-commit")]
    public async Task<IActionResult> ImportCommit([FromBody] ImportCommitRequest request, CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.CommitImportAsync(request, User.GetUserId(), ActorName, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }
}
