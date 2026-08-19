using DocGenerator.Api.Authorization;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// الإنابات التنفيذية: تسطير الإنابة (محامي الملف المنيب)، والاعتماد واختيار المحامي
/// (رئيس القسم)، والتسجيل أصولًا والإتمام بالبيع (محامي الملف المناب).
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
    private bool IsHead => Role == UserRole.Head;
    private bool CanManage => RolePermissions.CanManageDelegations(Role);
    private bool CanApprove => RolePermissions.CanApproveDelegations(Role);

    /// <summary>
    /// نفس قاعدة الوصول المطبقة على صفحة الملف (DocumentsController.CanAccess):
    /// مدير/مشرف (الكل)، رئيس قسم (فرعه)، محامٍ (ملفه فقط) — تمنع تسريب تشعبات ملفات الآخرين.
    /// </summary>
    private bool CanAccess(DocumentResponse doc)
    {
        if (HasFullAccess) return true;
        if (IsHead) return doc.BranchId == User.GetBranchId();
        return doc.CreatedById == User.GetUserId();
    }

    /// <summary>إنابات ملف (بطاقة «تشعبات الملف»): منيبة (المصدر) أو مناب (إنابته).</summary>
    [HttpGet("documents/{documentId:int}/delegations")]
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

    /// <summary>طلبات الإنابة المعلّقة لفرع رئيس القسم — نافذة «طلبات الإنابة والاستئنافات والمطالعات».</summary>
    [HttpGet("delegations/pending")]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        if (!CanApprove)
            return Forbid();
        var branchId = User.GetBranchId();
        if (branchId is null)
            return BadRequest(new { message = "رئيس القسم دون فرع لا يمكنه الاطلاع على طلبات الإنابة" });
        return Ok(await _delegations.ListPendingForHeadAsync(branchId.Value, ct));
    }

    /// <summary>
    /// اعتماد الإنابة واختيار المحامي المختص (تُنشأ الملف المناب تلقائيًا) — رئيس القسم.
    /// الإنابة الداخلية من رئيس قسم الفرع المنيب، والخارجية من رئيس قسم الفرع المناب.
    /// </summary>
    [HttpPost("delegations/{id:int}/assign")]
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
    }

    /// <summary>تسجيل الإنابة أصولًا (رقم أساس وتاريخ قيد الملف المناب) — محامي الملف المناب فقط.</summary>
    [HttpPost("delegations/{id:int}/register")]
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
