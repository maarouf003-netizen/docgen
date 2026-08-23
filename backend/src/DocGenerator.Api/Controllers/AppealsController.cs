using DocGenerator.Api.Authorization;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// الاستئنافات على الملفات التنفيذية: التسطير من محامي الملف المالك، والإسناد والنقل
/// من رئيس القسم، والقيد والحسم والشطب والتدوير والإجراءات المستقلة من المحامي المتابع.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class AppealsController : ControllerBase
{
    private readonly IDocumentAppealService _appeals;
    private readonly IDocumentService _documents;

    public AppealsController(IDocumentAppealService appeals, IDocumentService documents)
    {
        _appeals = appeals;
        _documents = documents;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool HasFullAccess => RolePermissions.HasFullAccess(Role);
    private bool IsHead => Role == UserRole.Head;
    private bool CanManageAppeals => RolePermissions.CanManageAppeals(Role);
    private bool CanAssignAppeals => RolePermissions.CanAssignAppeals(Role);

    /// <summary>قاعدة الوصول للملف (مطابقة DocumentsController.CanAccess): إدارة الكل، رئيس فرعه، محامٍ ملفه.</summary>
    private bool CanAccessDocument(DocGenerator.Application.DTOs.DocumentResponse doc)
    {
        if (HasFullAccess) return true;
        if (IsHead) return doc.BranchId == User.GetBranchId();
        return doc.CreatedById == User.GetUserId();
    }

    /// <summary>
    /// صلاحية رؤية الاستئناف: منشؤه، أو المحامي المسند إليه للمتابعة، أو رئيس قسم
    /// فرع الملف، أو الإدارة (قراءة مطلقة).
    /// </summary>
    private async Task<bool> CanViewAppealAsync(int appealId)
    {
        var appeal = await _appeals.GetEntityAsync(appealId);
        if (appeal is null) return false;
        if (HasFullAccess) return true;
        if (appeal.CreatedById == User.GetUserId() || appeal.AssignedLawyerId == User.GetUserId())
            return true;
        if (IsHead && appeal.Document.BranchId == User.GetBranchId()) return true;
        return false;
    }

    // ── استئنافات الملف ───────────────────────────────────────────────────

    /// <summary>استئنافات ملف (بطاقة «الاستئنافات» في وقوعات الملف).</summary>
    [HttpGet("documents/{documentId:int}/appeals")]
    public async Task<IActionResult> ListForDocument(int documentId, CancellationToken ct)
    {
        var doc = await _documents.GetAsync(documentId, ct);
        if (doc is null)
            return NotFound(new { message = "الملف غير موجود" });
        if (!await CanAccessOrFollowAsync(doc))
            return Forbid();
        try
        {
            return Ok(await _appeals.ListForDocumentAsync(documentId, ct));
        }
        catch (ArgumentException e)
        {
            return NotFound(new { message = e.Message });
        }
    }

    /// <summary>تسطير استئناف جديد — محامي الملف المالك فقط.</summary>
    [HttpPost("documents/{documentId:int}/appeals")]
    public async Task<IActionResult> Create(int documentId, [FromBody] UpsertAppealRequest request, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            var created = await _appeals.CreateAsync(documentId, request, User.GetUserId(), ActorName, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    // ── صفحة «الاستئنافات» ────────────────────────────────────────────────

    /// <summary>بحث/قائمة الاستئنافات بنطاق الرؤية: محامٍ (استئنافاته)، رئيس القسم (فرعه)، الإدارة (الكل).</summary>
    [HttpGet("appeals")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        if (HasFullAccess)
            return Ok(await _appeals.SearchAsync(q, status, null, null, page, perPage, ct));

        if (IsHead)
        {
            // رئيس القسم بلا فرع لا يملك نطاق رؤية محددًا — يُرفض بدل تسريب كل الاستئنافات.
            var headBranch = User.GetBranchId();
            if (headBranch is null)
                return BadRequest(new { message = "رئيس القسم دون فرع لا يمكنه الاطلاع على الاستئنافات" });
            return Ok(await _appeals.SearchAsync(q, status, headBranch, null, page, perPage, ct));
        }

        return Ok(await _appeals.SearchAsync(q, status, null, User.GetUserId(), page, perPage, ct));
    }

    /// <summary>تفاصيل استئناف كاملة.</summary>
    [HttpGet("appeals/{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        if (!await CanViewAppealAsync(id))
            return Forbid();
        var dto = await _appeals.GetAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>تعديل استئناف قبل الإسناد — المنشئ فقط.</summary>
    [HttpPut("appeals/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertAppealRequest request, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            var updated = await _appeals.UpdateAsync(id, request, User.GetUserId(), ActorName, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>حذف استئناف قبل الإسناد — المنشئ فقط.</summary>
    [HttpDelete("appeals/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            var ok = await _appeals.DeleteAsync(id, User.GetUserId(), ActorName, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    // ── القيد والحسم والشطب (المحامي المتابع) ─────────────────────────────

    /// <summary>تحديث حقول القيد (المحكمة/رقم الأساس/السنة/تاريخ الإقرار/النوع) — المحامي المتابع.</summary>
    [HttpPut("appeals/{id:int}/registration")]
    public async Task<IActionResult> UpdateRegistration(int id, [FromBody] UpdateAppealRegistrationRequest request, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            var updated = await _appeals.UpdateRegistrationAsync(id, request, User.GetUserId(), ActorName, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>حسم الاستئناف برقم قرار الحسم وتاريخه ومنطوقه ونتيجته — المحامي المتابع.</summary>
    [HttpPost("appeals/{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, [FromBody] DecideAppealRequest request, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            var updated = await _appeals.DecideAsync(id, request, User.GetUserId(), ActorName, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>شطب الاستئناف بتاريخ الشطب ورقم قرار الشطب — المحامي المتابع.</summary>
    [HttpPost("appeals/{id:int}/strike")]
    public async Task<IActionResult> Strike(int id, [FromBody] StrikeAppealRequest request, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            var updated = await _appeals.StrikeAsync(id, request, User.GetUserId(), ActorName, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    // ── الإسناد والنقل (رئيس القسم) ───────────────────────────────────────

    /// <summary>إسناد الاستئناف إلى محامي الفرع للمتابعة — رئيس القسم (فرعه).</summary>
    [HttpPost("appeals/{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignAppealRequest request, CancellationToken ct)
    {
        if (!CanAssignAppeals)
            return Forbid();
        try
        {
            var assigned = await _appeals.AssignAsync(id, request, User.GetUserId(), User.GetBranchId(), ActorName, ct);
            return assigned is null ? NotFound() : Ok(assigned);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>نقل استئناف مفرد بين محامي الفرع — رئيس القسم (فرعه).</summary>
    [HttpPost("appeals/{id:int}/transfer")]
    public async Task<IActionResult> Transfer(int id, [FromBody] TransferAppealRequest request, CancellationToken ct)
    {
        if (!CanAssignAppeals)
            return Forbid();
        try
        {
            var transferred = await _appeals.TransferAsync(id, request, User.GetUserId(), User.GetBranchId(), ActorName, ct);
            return transferred is null ? NotFound() : Ok(transferred);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>نقل كل استئنافات محامٍ إلى محامٍ آخر ضمن الفرع — رئيس القسم (فرعه).</summary>
    [HttpPost("appeals/transfer-all")]
    public async Task<IActionResult> TransferAll([FromBody] TransferAllAppealsRequest request, CancellationToken ct)
    {
        if (!CanAssignAppeals)
            return Forbid();
        try
        {
            var count = await _appeals.TransferAllAsync(request, User.GetBranchId(), ActorName, ct);
            return Ok(new { transferredCount = count });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>عدد استئنافات محامٍ ضمن فرع رئيس القسم — لمعاينة النقل الجملة.</summary>
    [HttpGet("appeals/owner/{lawyerId:int}/count")]
    public async Task<IActionResult> CountForOwner(int lawyerId, CancellationToken ct)
    {
        if (!CanAssignAppeals)
            return Forbid();
        try
        {
            return Ok(new { count = await _appeals.CountByAssigneeForHeadAsync(lawyerId, User.GetBranchId(), ct) });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    // ── تدوير رقم الأساس الاستئنافي ───────────────────────────────────────

    /// <summary>تاريخ أرقام الأساس الاستئنافية لكل السنوات.</summary>
    [HttpGet("appeals/{id:int}/base-numbers")]
    public async Task<IActionResult> GetBaseNumbers(int id, CancellationToken ct)
    {
        if (!await CanViewAppealAsync(id))
            return Forbid();
        try
        {
            return Ok(await _appeals.GetBaseNumberHistoryAsync(id, ct));
        }
        catch (ArgumentException e)
        {
            return NotFound(new { message = e.Message });
        }
    }

    /// <summary>إدخال/تدوير رقم الأساس الاستئنافي لسنة التدوير الحالية — المحامي المتابع أو المنشئ.</summary>
    [HttpPut("appeals/{id:int}/base-numbers")]
    public async Task<IActionResult> SaveBaseNumbers(int id, [FromBody] SaveAppealBaseNumbersRequest request, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            await _appeals.SaveBaseNumbersAsync(id, request, User.GetUserId(), ActorName, ct);
            return NoContent();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    // ── الإجراءات والملاحظات المستقلة للاستئناف ───────────────────────────

    /// <summary>إجراءات وملاحظات الاستئناف (قائمة مستقلة عن الملف الأساس).</summary>
    [HttpGet("appeals/{id:int}/actions")]
    public async Task<IActionResult> GetActions(int id, CancellationToken ct)
    {
        if (!await CanViewAppealAsync(id))
            return Forbid();
        try
        {
            return Ok(await _appeals.GetActionsAsync(id, ct));
        }
        catch (ArgumentException e)
        {
            return NotFound(new { message = e.Message });
        }
    }

    /// <summary>إضافة إجراء/ملاحظة على الاستئناف — المحامي المتابع.</summary>
    [HttpPost("appeals/{id:int}/actions")]
    public async Task<IActionResult> AddAction(int id, [FromBody] AddAppealActionRequest request, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            return Ok(await _appeals.AddActionAsync(id, request, User.GetUserId(), ActorName, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>تعديل إجراء على الاستئناف — المحامي المتابع.</summary>
    [HttpPut("appeals/{id:int}/actions/{actionId:int}")]
    public async Task<IActionResult> UpdateAction(int id, int actionId, [FromBody] UpdateAppealActionRequest request, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            var updated = await _appeals.UpdateActionAsync(id, actionId, request, User.GetUserId(), ActorName, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>حذف إجراء من الاستئناف — المحامي المتابع.</summary>
    [HttpDelete("appeals/{id:int}/actions/{actionId:int}")]
    public async Task<IActionResult> DeleteAction(int id, int actionId, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            var ok = await _appeals.DeleteActionAsync(id, actionId, User.GetUserId(), ActorName, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>إلغاء تذكير إجراء على الاستئناف — المحامي المتابع.</summary>
    [HttpDelete("appeals/{id:int}/actions/{actionId:int}/reminder")]
    public async Task<IActionResult> ClearReminder(int id, int actionId, CancellationToken ct)
    {
        if (!CanManageAppeals)
            return Forbid();
        try
        {
            var ok = await _appeals.ClearReminderAsync(id, actionId, User.GetUserId(), ActorName, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    /// <summary>تذكيرات إجراءات الاستئنافات التي يتابعها المحامي (بطاقة التذكيرات).</summary>
    [HttpGet("appeals/reminders")]
    [Authorize(Roles = "lawyer")]
    public async Task<IActionResult> Reminders(CancellationToken ct)
        => Ok(await _appeals.GetRemindersAsync(User.GetUserId(), ct));

    private async Task<bool> CanAccessOrFollowAsync(DocGenerator.Application.DTOs.DocumentResponse doc)
        => CanAccessDocument(doc) || await _appeals.IsAssignedFollowerAsync(doc.Id, User.GetUserId());
}
