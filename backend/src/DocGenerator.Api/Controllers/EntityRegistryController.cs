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
        [FromQuery] string? branchName,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        return Ok(await _registry.ListAsync(
            new EntityRegistryListQuery(q, governorate, status, IncludePending: true, page, perPage, BranchName: branchName), ct));
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
        [FromQuery] string? branchName,
        CancellationToken ct = default)
    {
        if (Role == UserRole.EntityManager)
            return Forbid();
        return Ok(await _registry.ListAsync(
            new EntityRegistryListQuery(q, governorate, null, IncludePending: true, 1, 50, IncludeInactive: false, BranchName: branchName), ct));
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

    /// <summary>اقتراح تعديل فردي من المحامي (يبقى بانتظار المراجعة — لا يزامن النصوص).</summary>
    [HttpPost("{id:int}/propose-edit")]
    public async Task<IActionResult> ProposeEdit(int id, [FromBody] ProposeEditRequest request, CancellationToken ct)
    {
        if (Role != UserRole.Lawyer)
            return Forbid();
        try
        {
            var updated = await _registry.ProposeEditAsync(id, request, Actor, ct);
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

    // ── قائمة المجموعات (الهويات الأم) وتوحيد التسمية N←1 (المدير/المشرف — بلا هجرة ملفات) ──

    /// <summary>قائمة المجموعات (الهويات الأم) مع ترقيم وبحث — للعرض المستقل وتوحيد التسمية/إدارة الفروع.</summary>
    [HttpGet("groups")]
    public async Task<IActionResult> ListGroups(
        [FromQuery] string? q,
        [FromQuery] string? governorate,
        [FromQuery] string? excludeIds,
        [FromQuery] string? includeIds,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        var excl = ParseIdList(excludeIds);
        var incl = ParseIdList(includeIds);
        return Ok(await _registry.ListGroupsAsync(new EntityGroupListQuery(q, governorate, page, perPage, excl, incl), Actor, ct));
    }

    private static List<int>? ParseIdList(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? null
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var v) ? v : (int?)null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

    /// <summary>قيود مجموعة واحدة — لرئيس القسم (محافظته فقط) وإدارة الفروع.</summary>
    [HttpGet("groups/{groupId:int}/entries")]
    public async Task<IActionResult> ListGroupEntries(int groupId, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.ListEntriesByGroupAsync(groupId, Actor, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>أقرب المشابهات لجهة محددة (تبويب «كافة الجهات» عند تحديد جهة واحدة) — المدير/المشرف فقط.</summary>
    [HttpGet("groups/{groupId:int}/similar-to")]
    public async Task<IActionResult> SimilarTo(int groupId, [FromQuery] double? threshold, [FromQuery] int? maxResults, CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.FindSimilarToGroupAsync(groupId, threshold ?? 0, maxResults ?? 0, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>معاينة توحيد التسمية N←1 (المدير/المشرف — بلا هجرة ملفات).</summary>
    [HttpPost("groups/unify-preview")]
    public async Task<IActionResult> UnifyPreview([FromBody] UnifyNamesPreviewRequest request, CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.PreviewUnifyAsync(request, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>تنفيذ توحيد التسمية N←1 (المدير/المشرف — ينقل القيود ويعطّل المجموعات الممتصة بلا هجرة ملفات).</summary>
    [HttpPost("groups/unify")]
    public async Task<IActionResult> Unify([FromBody] UnifyNamesRequest request, CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.UnifyNamesAsync(request, Actor, ct));
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

    // ── إعادة تسمية هوية أم (المدير/المشرف — بمرسوم إلزامي) ──

    /// <summary>معاينة إعادة تسمية هوية أم واحدة قبل الاعتماد (تعدد الملفات المتأثرة وفروعها).</summary>
    [HttpPost("groups/{groupId:int}/rename-preview")]
    public async Task<IActionResult> RenameGroupPreview(int groupId, [FromBody] RenameGroupPreviewRequest request, CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        if (request.GroupId != groupId)
            return BadRequest(new { message = "تعارض معرف المجموعة في المسار" });
        try
        {
            return Ok(await _registry.PreviewRenameGroupAsync(request, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>تنفيذ إعادة تسمية هوية أم واحدة بموجب مرجع (المدير/المشرف).</summary>
    [HttpPost("groups/{groupId:int}/rename")]
    public async Task<IActionResult> RenameGroup(int groupId, [FromBody] RenameGroupRequest request, CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        if (request.GroupId != groupId)
            return BadRequest(new { message = "تعارض معرف المجموعة في المسار" });
        try
        {
            return Ok(await _registry.RenameGroupAsync(request, Actor, ct));
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

    // ── الحلول (إلغاء عدة هويات أم واستبدالها بهوية جديدة — المدير/المشرف) ──

    /// <summary>معاينة الحلول (إلغاء واستبدال) قبل الاعتماد.</summary>
    [HttpPost("groups/abolish-preview")]
    public async Task<IActionResult> AbolishPreview([FromBody] AbolishReplacePreviewRequest request, CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.PreviewAbolishAndReplaceAsync(request, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>تنفيذ الحلول: إلغاء عدة هويات أم واستبدالها بهوية أم جديدة (المدير/المشرف).</summary>
    [HttpPost("groups/abolish-and-replace")]
    public async Task<IActionResult> AbolishAndReplace([FromBody] AbolishAndReplaceRequest request, CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.AbolishAndReplaceAsync(request, Actor, ct));
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

    /// <summary>سجل تغييرات الجهات — مصدره PublicEntityChangeEvent فقط (د5 §7)؛ رئيس القسم يرى محافظته (جبر خادمي).</summary>
    [HttpGet("change-events")]
    public async Task<IActionResult> ListChangeEvents(
        [FromQuery] string? governorate,
        [FromQuery] string? actionKind,
        [FromQuery] int? actorUserId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        return Ok(await _registry.ListChangeEventsAsync(
            new EntityChangeEventQuery(governorate, actionKind, actorUserId, from, to, page, perPage), Actor, ct));
    }

    /// <summary>تصدير سجل التغييرات إلى Excel (نفس فلاتر القائمة — رئيس القسم يرى محافظته).</summary>
    [HttpGet("change-events/export")]
    public async Task<IActionResult> ExportChangeEvents(
        [FromQuery] string? governorate,
        [FromQuery] string? actionKind,
        [FromQuery] int? actorUserId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct = default)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        var bytes = await _registry.ExportChangeEventsAsync(
            new EntityChangeEventQuery(governorate, actionKind, actorUserId, from, to, 1, 5000), Actor, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "change-events.xlsx");
    }

    // ── عمليات فروع رئيس القسم (تعديل التسمية/دمج/إلغاء/توحيد — ضمن محافظته) ──

    /// <summary>معاينة موحدة لأي عملية فرع قبل الاعتماد (rename/merge/abolish/unify) — بلا كتابة.</summary>
    [HttpPost("groups/{groupId:int}/branches/preview")]
    public async Task<IActionResult> PreviewBranchAction(int groupId, [FromBody] PreviewBranchActionRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.PreviewBranchActionAsync(groupId, request, Actor, ct));
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

    /// <summary>تعديل تسمية فرع ضمن محافظة رئيس القسم (بلا مرسوم) — يزامن لقطات الفروع (S7).</summary>
    [HttpPost("groups/{groupId:int}/branches/{entryId:int}/rename-branch")]
    public async Task<IActionResult> RenameBranch(int groupId, int entryId, [FromBody] RenameBranchRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.RenameBranchAsync(groupId, entryId, request, Actor, ct));
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

    /// <summary>دمج فرعين نشطين في نفس الهوية الأم والمحافظة (ضمن نطاق رئيس القسم).</summary>
    [HttpPost("groups/{groupId:int}/branches/merge")]
    public async Task<IActionResult> MergeBranches(int groupId, [FromBody] MergeBranchesRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.MergeBranchesAsync(groupId, request, Actor, ct));
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

    /// <summary>إلغاء فرع: تعطيل مباشر لصفر روابط أو دمج ضمني مع فرع هدف (S4) — ضمن نطاق رئيس القسم.</summary>
    [HttpPost("groups/{groupId:int}/branches/{entryId:int}/abolish")]
    public async Task<IActionResult> AbolishBranch(int groupId, int entryId, [FromBody] AbolishBranchRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.AbolishBranchAsync(groupId, entryId, request, Actor, ct));
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

    /// <summary>توحيد تسميات عدة فروع في فرع ناجٍ (اختياريًا مع تصحيح كتابة اسمه) — ضمن نطاق رئيس القسم.</summary>
    [HttpPost("groups/{groupId:int}/branches/unify")]
    public async Task<IActionResult> UnifyBranches(int groupId, [FromBody] UnifyBranchesRequest request, CancellationToken ct)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        try
        {
            return Ok(await _registry.UnifyBranchesAsync(groupId, request, Actor, ct));
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

    // ── اقتراح تعديل الجهة الأم (رئيس القسم → مدير/مشرف) ──

    /// <summary>اقتراح تعديل بيانات قيد «الجهة الأم» من رئيس القسم (بلا أي كتابة على القيد).</summary>
    [HttpPost("entries/{entryId:int}/suggest-parent-edit")]
    public async Task<IActionResult> SuggestParentEdit(int entryId, [FromBody] SuggestParentEditRequest request, CancellationToken ct)
    {
        if (Role != UserRole.Head)
            return Forbid();
        try
        {
            return Ok(await _registry.SuggestParentEditAsync(entryId, request, Actor, ct));
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

    /// <summary>قائمة اقتراحات تعديل الجهة الأم — المدير/المشرف (الكل) ورئيس القسم (اقتراحاته فقط).</summary>
    [HttpGet("parent-edit-suggestions")]
    public async Task<IActionResult> ListParentEditSuggestions(
        [FromQuery] string? status,
        [FromQuery] int? groupId,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        if (!RolePermissions.CanManageEntityRegistry(Role))
            return Forbid();
        var result = await _registry.ListParentEditSuggestionsAsync(
            new ParentEditSuggestionListQuery(status, groupId, page, perPage), Actor, ct);
        return Ok(new ParentEditSuggestionListResponse(result.Items, result.TotalCount));
    }

    /// <summary>قبول/رفض اقتراح تعديل الجهة الأم (المدير/المشرف فقط — سبب إلزامي عند الرفض).</summary>
    [HttpPost("parent-edit-suggestions/{suggestionId:int}/review")]
    public async Task<IActionResult> ReviewParentEditSuggestion(int suggestionId, [FromBody] ReviewParentEditSuggestionRequest request, CancellationToken ct)
    {
        if (!RolePermissions.HasFullAccess(Role))
            return Forbid();
        try
        {
            var dto = await _registry.ReviewParentEditSuggestionAsync(suggestionId, request, Actor, ct);
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

    /// <summary>سحب ذاتي لاقتراح معلّق (رئيس القسم المنشئ نفسه فقط).</summary>
    [HttpPost("parent-edit-suggestions/{suggestionId:int}/withdraw")]
    public async Task<IActionResult> WithdrawParentEditSuggestion(int suggestionId, CancellationToken ct)
    {
        if (Role != UserRole.Head)
            return Forbid();
        try
        {
            var dto = await _registry.WithdrawParentEditSuggestionAsync(suggestionId, Actor, ct);
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
}
