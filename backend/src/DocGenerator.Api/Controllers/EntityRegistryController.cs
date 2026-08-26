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

    /// <summary>
    /// بحث السجل لنافذة الإدخال: متاح لكل الطاقم عدا دور المندوب؛ قيود الانتظار
    /// تظهر للمحامي كي يستطيع ربطها على ملفه مع تمييزها بصريًا (§5.3/د4)،
    /// ولا تظهر لأي بوابٍ قبل الاعتماد. القيود الموقوفة لا تصلح للربط فتُستبعد.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? governorate,
        CancellationToken ct = default)
    {
        if (Role == UserRole.EntityManager)
            return Forbid();
        return Ok(await _registry.ListAsync(
            new EntityRegistryListQuery(q, governorate, null, IncludePending: true, 1, 50, IncludeInactive: false), ct));
    }

    /// <summary>
    /// إنشاء قيد جهة نهائي مباشر: رئيس قسم (ضمن محافظته)/مدير/مشرف، **أو محامٍ**
    /// وفق نموذج الحوكمة الجديد — يدخل نهائيًا فورًا ويبقى بانتظار مراجعة رئيس
    /// محافظته مع تنبيه له.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePublicEntityRequest request, CancellationToken ct)
    {
        if (Role == UserRole.EntityManager)
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

    // ── مراجعة سجل الجهات العامة الممثلة (نموذج الحوكمة الجديد) ──

    /// <summary>
    /// قائمة «بحاجة مراجعة»: رئيس القسم يرى محافظة فرعه حصرًا، والمدير/المشرف
    /// يرىان كل السجل. تُستخدم لبطاقة التنبيه وصفحة المراجعة.
    /// </summary>
    [HttpGet("pending-review")]
    public async Task<IActionResult> PendingReview(CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        return Ok(await _registry.ListNeedsReviewAsync(Actor, ct));
    }

    /// <summary>اعتماد قيد كما هو: يقفل مراجعته دون تعديل ودون إشعار للمُدخِل.</summary>
    [HttpPost("{id:int}/approve-review")]
    public async Task<IActionResult> ApproveReview(int id, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            var dto = await _registry.ApproveReviewAsync(id, Actor, ct);
            return dto is null ? NotFound() : Ok(dto);
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

    /// <summary>نقل قيد من هوية أم إلى أخرى أو طيه في قيد مطابق (د3).</summary>
    [HttpPost("{id:int}/move")]
    public async Task<IActionResult> MoveEntry(int id, [FromBody] MoveEntryRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.MoveEntryAsync(id, request, Actor, ct));
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

    /// <summary>نقل جميع قيود هوية أم إلى هوية أم أخرى (د3 — الوضع أ فقط).</summary>
    [HttpPost("move-all")]
    public async Task<IActionResult> MoveAllEntries([FromBody] MoveAllEntriesRequest request, CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.MoveAllEntriesAsync(request, Actor, ct));
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

    // ── الدمج N←1 (د5 §4) — صلاحية مخصصة: CanMergeEntities ──

    /// <summary>معاينة دمج جهات متعددة في هوية واحدة (د5 §4).</summary>
    [HttpPost("merge-preview")]
    public async Task<IActionResult> MergePreview([FromBody] MergePreviewRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanMergeEntities(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.PreviewMergeAsync(request, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>تنفيذ اعتماد الدمج (د5 §4).</summary>
    [HttpPost("merge-commit")]
    public async Task<IActionResult> MergeCommit([FromBody] MergeCommitRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanMergeEntities(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.CommitMergeAsync(request, Actor, ct));
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
}
