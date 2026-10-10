using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// الإنابات التنفيذية: تسطير الإنابة (محامي الملف المنيب)، والاعتماد واختيار المحامي
/// (مالك الدائرة المنابة: رئيس الشعبة لدوائر شعبته ورئيس القسم لدوائر قسمه — §7.1)،
/// والتسجيل أصولًا والإتمام بالبيع (محامي الملف المناب).
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class DelegationsController : ControllerBase
{
    private readonly IDocumentDelegationService _delegations;
    private readonly IDocumentService _documents;

    public DelegationsController(IDocumentDelegationService delegations, IDocumentService documents)
    {
        _delegations = delegations;
        _documents = documents;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool HasFullAccess => RolePermissions.HasFullAccess(Role);
    private bool IsHeadOrSubHead => RolePermissions.IsHeadOrSubHead(Role);
    private bool CanManage => RolePermissions.CanManageDelegations(Role);
    private bool CanApprove => RolePermissions.CanApproveDelegations(Role);

    /// <summary>
    /// نطاق الرؤية (§7.1): رئيس بلا فرع مرفوض، ورئيس شعبة بلا شعبة مرفوض —
    /// بلا تدهور لنطاق أوسع.
    /// </summary>
    private ActionResult? RequireApproverScope(out int branchId, out int? ownerSectionId)
    {
        branchId = 0;
        ownerSectionId = null;
        var branch = User.GetBranchId();
        if (branch is null)
            return BadRequest(new { message = "الرئيس دون فرع لا يمكنه الاطلاع على طلبات الإنابة" });
        branchId = branch.Value;
        if (Role == UserRole.SubHead)
        {
            var section = User.GetSectionId();
            if (section is null)
                return Forbid();
            ownerSectionId = section;
        }
        return null;
    }

    /// <summary>
    /// قاعدة الوصول لبطاقة «تشعبات الملف»: كقاعدة صفحة الملف (مدير/مشرف الكل؛
    /// محامٍ ملفه فقط؛ رئيس قسم ملفات دوائر القسم وبلا دائرة في فرعه؛ رئيس
    /// شعبة ملفات دوائر شعبته) — بلا الاستثناء القرائي للإحالة (§2.22) وبلا
    /// المتابع: تضييق مقصود، فالاستثناء يفتح الملف ومسار استئنافاته لا تشعباته.
    /// </summary>
    private bool CanAccess(DocumentResponse doc)
    {
        if (HasFullAccess) return true;
        if (Role == UserRole.Lawyer)
            return doc.CreatedById == User.GetUserId();
        if (doc.BranchId != User.GetBranchId())
            return false;
        if (Role == UserRole.Head)
            return doc.ExecutionCircuitId is null || !doc.SectionId.HasValue;
        if (Role == UserRole.SubHead)
            return doc.SectionId.HasValue && doc.SectionId == User.GetSectionId();
        return false;
    }

    /// <summary>إنابات ملف (بطاقة «تشعبات الملف»): منيبة (المصدر) أو مناب (إنابته) — تشمل بطاقة المناب (§5.7).</summary>
    [HttpGet("documents/{documentId:int}/delegations")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> ListForDocument(int documentId, CancellationToken ct)
    {
        var doc = await _documents.GetAsync(documentId, ct);
        if (doc is null)
            return NotFound(new { message = "الملف غير موجود" });
        if (!CanAccess(doc))
            return Forbid();
        try
        {
            return Ok(await _delegations.ListForDocumentAsync(documentId, ct));
        }
        catch (ArgumentException e)
        {
            return NotFound(new { message = e.Message });
        }
    }

    /// <summary>تسطير إنابة جديدة على ملف منيب — محامي الملف المالك فقط.</summary>
    [HttpPost("documents/{documentId:int}/delegations")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Create(int documentId, [FromBody] UpsertDelegationRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var created = await _delegations.CreateAsync(documentId, request, User.GetUserId(), ActorName, ct);
            return CreatedAtAction(nameof(ListForDocument), new { documentId }, created);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>تعديل إنابة معلّقة — محامي الملف المنيب المالك فقط.</summary>
    [HttpPut("delegations/{id:int}")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertDelegationRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var updated = await _delegations.UpdateAsync(id, request, User.GetUserId(), ActorName, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>حذف إنابة معلّقة — محامي الملف المنيب المالك فقط.</summary>
    [HttpDelete("delegations/{id:int}")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var ok = await _delegations.DeleteAsync(id, User.GetUserId(), ActorName, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>
    /// طلبات الإنابة المعلّقة لنطاق الرئيس (§7.1 + §7.4): رئيس القسم (دوائر
    /// القسم وبلا دائرة + الخارجية غير الموجَّهة في فرعه)، ورئيس الشعبة (دوائر
    /// شعبته + الخارجية الموجَّهة لشعبته). `rejectedOnly` لفلتر
    /// «مرفوض بانتظار التصحيح».
    /// </summary>
    [HttpGet("delegations/pending")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Pending([FromQuery] bool rejectedOnly = false, CancellationToken ct = default)
    {
        if (!CanApprove)
            return Forbid();
        var error = RequireApproverScope(out var branchId, out var ownerSectionId);
        if (error is not null)
            return error;
        return Ok(await _delegations.ListPendingForHeadAsync(branchId, ownerSectionId, rejectedOnly, ct));
    }

    /// <summary>
    /// عدّادا طلبات الإنابة المعلّقة لنطاق الرئيس — شارة خفيفة (استطلاع دوري)
    /// دون تحميل القائمة: القابلة للاعتماد + «مرفوض بانتظار التصحيح» (§7.4).
    /// </summary>
    [HttpGet("delegations/pending-count")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> PendingCount(CancellationToken ct)
    {
        if (!CanApprove)
            return Forbid();
        var error = RequireApproverScope(out var branchId, out var ownerSectionId);
        if (error is not null)
            return error;
        return Ok(new
        {
            count = await _delegations.CountPendingForHeadAsync(branchId, ownerSectionId, false, ct),
            rejectedCount = await _delegations.CountPendingForHeadAsync(branchId, ownerSectionId, true, ct),
        });
    }

    /// <summary>
    /// اعتماد الإنابة واختيار المحامي المختص (تُنشأ الملف المناب تلقائيًا) —
    /// الاعتماد لمالك الدائرة المنابة (§7.1): رئيس الشعبة لدوائر شعبته (ومنه
    /// الخارجية الموجَّهة لشعبته)، ورئيس القسم لدوائر القسم وبلا دائرة
    /// والخارجية غير الموجَّهة في فرعه.
    /// </summary>
    [HttpPost("delegations/{id:int}/assign")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignDelegationRequest request, CancellationToken ct)
    {
        if (!CanApprove)
            return Forbid();
        try
        {
            var assigned = await _delegations.AssignAsync(id, request, User.GetUserId(), User.GetBranchId(), ActorName, ct);
            return assigned is null ? NotFound() : Ok(assigned);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
    }

    /// <summary>توجيه إنابة خارجية معلّقة لشعبة في الفرع المناب — رئيس قسم الفرع المناب فقط (§7.3).</summary>
    [HttpPost("delegations/{id:int}/redirect")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Redirect(int id, [FromBody] RedirectDelegationRequest request, CancellationToken ct)
    {
        if (!CanApprove)
            return Forbid();
        try
        {
            var redirected = await _delegations.RedirectToSectionAsync(id, request, User.GetUserId(), ActorName, ct);
            return redirected is null ? NotFound() : Ok(redirected);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
    }

    /// <summary>التراجع عن توجيه الشعبة قبل الإسناد — رئيس قسم فرع الاعتماد فقط (§7.3).</summary>
    [HttpPost("delegations/{id:int}/recall-redirect")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> RecallRedirect(int id, CancellationToken ct)
    {
        if (!CanApprove)
            return Forbid();
        try
        {
            var recalled = await _delegations.RecallRedirectAsync(id, User.GetUserId(), ActorName, ct);
            return recalled is null ? NotFound() : Ok(recalled);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
    }

    /// <summary>رفض الدائرة الخطأ برسالة تُعيد المحامي للتصحيح (§7.4) — المعتمد الحالي فقط.</summary>
    [HttpPost("delegations/{id:int}/reject")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectDelegationRequest request, CancellationToken ct)
    {
        if (!CanApprove)
            return Forbid();
        try
        {
            var rejected = await _delegations.RejectAsync(id, request ?? new RejectDelegationRequest(null), User.GetUserId(), ActorName, ct);
            return rejected is null ? NotFound() : Ok(rejected);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
    }

    /// <summary>تسجيل الإنابة أصولًا (رقم أساس وتاريخ قيد الملف المناب) — محامي الملف المناب فقط.</summary>
    [HttpPost("delegations/{id:int}/register")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Register(int id, [FromBody] RegisterDelegationRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var registered = await _delegations.RegisterAsync(id, request, User.GetUserId(), ActorName, ct);
            return registered is null ? NotFound() : Ok(registered);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>
    /// إتمام الإنابة: بيع الأموال موضوع الإنابة بالمزاد العلني (بدل المبيع لكل أصل) وتاريخ إعادة
    /// الملف للدائرة المنيبة — محامي الملف المناب فقط. يُصبح الملف المناب «منفذ إنابة».
    /// </summary>
    [HttpPost("delegations/{id:int}/complete")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteDelegationRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        try
        {
            var completed = await _delegations.CompleteAsync(id, request, User.GetUserId(), ActorName, ct);
            return completed is null ? NotFound() : Ok(completed);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }
}
