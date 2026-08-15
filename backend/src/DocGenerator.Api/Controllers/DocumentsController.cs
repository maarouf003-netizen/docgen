using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private const string WordContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IDocumentService _documents;
    private readonly IWordDocumentGenerator _generator;
    private readonly IExcelExportService _excel;

    public DocumentsController(IDocumentService documents, IWordDocumentGenerator generator, IExcelExportService excel)
    {
        _documents = documents;
        _generator = generator;
        _excel = excel;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool HasFullAccess => RolePermissions.HasFullAccess(Role);
    private bool IsHead => Role == UserRole.Head;
    private bool CanViewCounters => RolePermissions.CanViewCounters(Role);
    private bool CanSearchByLawyer => RolePermissions.CanSearchByLawyer(Role);
    private bool CanEdit => RolePermissions.CanEditDocuments(Role);
    private bool CanChangeStatus => RolePermissions.CanChangeDocumentStatus(Role);
    private bool CanDelete => RolePermissions.CanDeleteDocuments(Role);
    private bool CanManageActions => RolePermissions.CanManageExecutionActions(Role);
    private bool CanRotate => RolePermissions.CanRotate(Role);

    private DocumentResponse Sanitize(DocumentResponse doc)
    {
        if (CanViewCounters) return doc;
        doc.ViewCount = 0;
        doc.PrintCount = 0;
        return doc;
    }

    private bool CanAccess(DocumentResponse doc)
    {
        if (HasFullAccess) return true;
        if (IsHead) return doc.BranchId == User.GetBranchId();
        return doc.CreatedById == User.GetUserId();
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] string? applicant, [FromQuery] string? court,
        [FromQuery] string? lawyer, [FromQuery] string? branch,
        [FromQuery] string? administrativeBranch, [FromQuery] string? executedEntity,
        [FromQuery] string? publicEntityBranch,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // البحث/الفلترة باسم المحامي محصور برئيس القسم/المدير/المشرف.
        if (!string.IsNullOrWhiteSpace(lawyer) && !CanSearchByLawyer)
            return Forbid();

        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = HasFullAccess || IsHead ? (int?)null : User.GetUserId();

        var result = await _documents.SearchAsync(q, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch,
            page, perPage, visibleBranch, visibleUser, ct);
        if (!CanViewCounters)
            result.Items = result.Items.Select(Sanitize).ToList();
        return Ok(result);
    }

    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions(
        [FromQuery] string? status, [FromQuery] string? applicant,
        [FromQuery] string? court, [FromQuery] string? lawyer,
        [FromQuery] string? branch, [FromQuery] string? administrativeBranch,
        [FromQuery] string? executedEntity, [FromQuery] string? publicEntityBranch,
        CancellationToken ct)
    {
        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = HasFullAccess || IsHead ? (int?)null : User.GetUserId();
        var options = await _documents.GetFilterOptionsAsync(status, applicant, court, lawyer, branch,
            administrativeBranch, executedEntity, publicEntityBranch, visibleBranch, visibleUser, ct);
        return Ok(new
        {
            applicants = options.Applicants,
            courts = options.Courts,
            lawyers = CanSearchByLawyer ? options.Lawyers : new List<string>(),
            administrativeBranches = options.AdministrativeBranches,
            branches = options.Branches,
            executedEntities = options.ExecutedEntities,
            publicEntityBranches = options.PublicEntityBranches,
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] string? applicant, [FromQuery] string? court,
        [FromQuery] string? lawyer, [FromQuery] string? branch,
        [FromQuery] string? administrativeBranch, [FromQuery] string? executedEntity,
        [FromQuery] string? publicEntityBranch, CancellationToken ct)
    {
        // التصدير يحترم نفس أذونات الفلترة: البحث باسم المحامي محصور برئيس القسم/المدير/المشرف.
        if (!string.IsNullOrWhiteSpace(lawyer) && !CanSearchByLawyer)
            return Forbid();

        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = HasFullAccess || IsHead ? (int?)null : User.GetUserId();

        var items = await _documents.ExportAsync(q, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch,
            visibleBranch, visibleUser, ct);
        if (!CanViewCounters)
            items = items.Select(Sanitize).ToList();

        var bytes = _excel.BuildDocumentsWorkbook(
            items,
            includeAdministrativeBranch: RolePermissions.CanSeeAdministrativeBranch(Role),
            includeAssignedLawyer: RolePermissions.CanSeeAssignedLawyer(Role),
            includeViewCount: CanViewCounters);

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"الملفات التنفيذية {DateTime.Now:yyyy-MM-dd}.xlsx");
    }

    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeleted(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // رؤية المحذوفات: محامٍ (ملفاته) / رئيس قسم (فرعه) / مشرف (الكل)، والمدير لا يراها.
        if (!RolePermissions.CanViewDeletedDocuments(Role))
            return Forbid();

        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = IsHead ? (int?)null : User.GetUserId();

        var result = await _documents.SearchDeletedAsync(q, page, perPage, visibleBranch, visibleUser, ct);
        return Ok(result);
    }

    [HttpGet("struck-off")]
    public async Task<IActionResult> GetStruckOff(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // رؤية الملفات المشطوبة في وضع «منفذ عليه» بنفس صلاحيات المحذوفات:
        // محامٍ (ملفاته) / رئيس قسم (فرعه) / مشرف (الكل)، والمدير لا يراها.
        if (!RolePermissions.CanViewDeletedDocuments(Role))
            return Forbid();

        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = IsHead ? (int?)null : User.GetUserId();

        var result = await _documents.SearchStruckOffAsync(q, page, perPage, visibleBranch, visibleUser, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();
        return Ok(Sanitize(doc));
    }

    [HttpGet("{id:int}/base-numbers")]
    public async Task<IActionResult> GetBaseNumberHistory(int id, CancellationToken ct)
    {
        // تاريخ أرقام الأساس — بنفس صلاحيات العرض المفصّل للملف.
        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();
        return Ok(await _documents.GetBaseNumberHistoryAsync(id, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DocumentUpsertRequest request, CancellationToken ct)
    {
        // إدخال الملفات محصور بالمحامي: لا إدخال لرئيس القسم، وقراءة مطلقة للمدير/المشرف.
        if (!CanEdit)
            return Forbid();

        try
        {
            var doc = await _documents.CreateAsync(request, User.GetUserId(), ActorName, User.GetBranchId(), ct);
            return CreatedAtAction(nameof(Get), new { id = doc.Id }, Sanitize(doc));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] DocumentUpsertRequest request, CancellationToken ct)
    {
        // التعديل محصور بالمحامي (للملفات التي يملكها).
        if (!CanEdit) return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        var updated = await _documents.UpdateAsync(id, request, ActorName, User.GetUserId(), ct);
        return updated is null ? NotFound() : Ok(Sanitize(updated));
    }

    [HttpGet("rotate")]
    public async Task<IActionResult> GetRotationList([FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // تدوير أرقام الأساس للمحامي فقط — على ملفاته.
        if (!CanRotate)
            return Forbid();

        return Ok(await _documents.GetRotationListAsync(User.GetUserId(), page, perPage, ct));
    }

    [HttpPut("rotate")]
    public async Task<IActionResult> SaveBaseNumbers([FromBody] SaveBaseNumbersRequest request, CancellationToken ct)
    {
        // تدوير أرقام الأساس للمحامي فقط — على ملفاته.
        if (!CanRotate)
            return Forbid();

        try
        {
            await _documents.SaveBaseNumbersAsync(User.GetUserId(), request.Entries, ActorName, ct);
            return NoContent();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        // الحذف المنطقي محصور بالمحامي صاحب الملف.
        if (!CanDelete)
            return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        return await _documents.DeleteAsync(id, ActorName, ct) ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id, CancellationToken ct)
    {
        // استعادة المحذوف منطقياً من اختصاص المحامي صاحب الملف فقط.
        if (!CanDelete)
            return Forbid();

        var doc = await _documents.GetDeletedAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        return await _documents.RestoreAsync(id, ActorName, ct)
            ? Ok(new { message = "تمت استعادة المستند" })
            : NotFound();
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] StatusRequest request, CancellationToken ct)
    {
        // تغيير حالة المستند محصور بالمحامي (للملفات التي يملكها).
        if (!CanChangeStatus) return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        try
        {
            var ok = await _documents.UpdateStatusAsync(id, request.Status, request.Fields ?? new(), ActorName, ct);
            return ok ? Ok(new { message = "تم تحديث الحالة" }) : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost("{id:int}/revert-status")]
    public async Task<IActionResult> RevertStatus(int id, [FromBody] StatusRequest request, CancellationToken ct)
    {
        // التراجع عن الحالة (من تريث/منفذ بالتسوية/منفذ جبريا إلى متداول) محصور بالمحامي
        // (للملفات التي يملكها) — بموجب كتاب الجهة العامة بالسير بالملف.
        if (!CanChangeStatus) return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        try
        {
            var ok = await _documents.RevertStatusAsync(id, request.Fields ?? new(), ActorName, ct);
            return ok ? Ok(new { message = "عُد الملف إلى المتداول" }) : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost("{id:int}/executed-status")]
    public async Task<IActionResult> SetExecutedStatus(int id, [FromBody] ExecutedStatusRequest request, CancellationToken ct)
    {
        // تغيير حالة وضع «الجهة العامة منفذ عليها» محصور بالمحامي (للملفات التي يملكها) —
        // يعمل على ملفات صفة executed فقط.
        if (!CanChangeStatus) return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        try
        {
            var ok = await _documents.UpdateExecutedStatusAsync(id, request.Status, request, ActorName, ct);
            return ok ? Ok(new { message = "تم تحديث حالة وضع «الجهة العامة منفذ عليها»" }) : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost("{id:int}/restore-struck-off")]
    public async Task<IActionResult> RestoreStruckOff(int id, [FromBody] RenewalRequest request, CancellationToken ct)
    {
        // إعادة ملف مشطوب إلى المتداول من اختصاص المحامي صاحب الملف فقط
        // (بذات حكم الاستعادة في المحذوفات).
        if (!CanDelete) return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        try
        {
            return await _documents.RestoreStruckOffAsync(id, request, ActorName, ct)
                ? Ok(new { message = "أعيد الملف المشطوب إلى المتداول" })
                : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost("{id:int}/view")]
    public async Task<IActionResult> TrackView(int id, CancellationToken ct)
    {
        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();
        await _documents.IncrementViewCountAsync(id, ct);
        return Ok();
    }

    [HttpGet("{id:int}/generate")]
    public async Task<IActionResult> Generate(
        int id,
        [FromQuery] string template,
        [FromQuery] int recipient = 0,
        [FromQuery] int[]? estateIds = null,
        [FromQuery] int heirId = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(template))
            return BadRequest(new { message = "يرجى تحديد نوع المستند المطلوب توليده" });

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        // توليد المستندات محصور بنظام «طالبة تنفيذ»: ملفات عائلة وضع «منفذ عليه» لا تُولَّد.
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            return BadRequest(new { message = "لا يُولَّد مستند لملفات وضع «الجهة العامة منفذ عليها» و«عرض وايداع»" });

        try
        {
            var result = await _generator.GenerateAsync(id, template, recipient, estateIds, heirId, ct);
            return File(result.Bytes, WordContentType, result.FileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (FileNotFoundException)
        {
            // لا يُكشف مسار قالب الخادم للمستخدم؛ تُرجع رسالة عامة.
            return StatusCode(500, new { message = "القالب غير متوفر على الخادم" });
        }
    }

    [HttpGet("owner/{lawyerId:int}/count")]
    public async Task<IActionResult> CountFilesByOwner(int lawyerId, CancellationToken ct)
    {
        // عدد ملفات المحامي (معاينة قبل النقل الجماعي) — رئيس القسم (ضمن فرعه) فقط.
        if (!RolePermissions.CanTransferDocuments(Role))
            return Forbid();

        // رئيس القسم بلا فرع لا يملك نطاقًا صالحًا للنقل (يُمنع صراحةً كبقية عمليات الفرع).
        var scopeBranchId = User.GetBranchId();
        if (scopeBranchId is null)
            return Forbid();

        try
        {
            var count = await _documents.CountFilesByOwnerAsync(lawyerId, scopeBranchId, ct);
            return Ok(new { count });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost("transfer-all")]
    public async Task<IActionResult> TransferAll([FromBody] TransferAllRequest request, CancellationToken ct)
    {
        // نقل كامل ملفات محامٍ إلى محامٍ آخر بجميع الحالات — رئيس القسم (ضمن فرعه) فقط.
        if (!RolePermissions.CanTransferDocuments(Role))
            return Forbid();

        // رئيس القسم بلا فرع لا يملك نطاقًا صالحًا للنقل (يُمنع صراحةً كبقية عمليات الفرع).
        var scopeBranchId = User.GetBranchId();
        if (scopeBranchId is null)
            return Forbid();

        try
        {
            var transferredCount = await _documents.TransferAllAsync(
                request.SourceLawyerId, request.TargetLawyerId, scopeBranchId, ActorName, ct);
            return Ok(new { transferredCount });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost("{id:int}/transfer")]
    public async Task<IActionResult> Transfer(int id, [FromBody] TransferDocumentRequest request, CancellationToken ct)
    {
        // نقل الملفات بين المحامين — رئيس القسم (ضمن فرعه) فقط.
        if (!RolePermissions.CanTransferDocuments(Role))
            return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        try
        {
            var updated = await _documents.TransferAsync(id, request.TargetLawyerId, ActorName, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DocumentConflictException e)
        {
            return Conflict(new { message = e.Message });
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpGet("{id:int}/actions")]
    public async Task<IActionResult> GetActions(int id, CancellationToken ct)
    {
        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        var actions = await _documents.GetExecutionActionsAsync(id, ct);
        return Ok(actions);
    }

    [HttpPost("{id:int}/actions")]
    public async Task<IActionResult> AddAction(int id, [FromBody] AddExecutionActionRequest request, CancellationToken ct)
    {
        // الإضافة للمحامي فقط
        if (!CanManageActions)
            return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        try
        {
            var action = await _documents.AddExecutionActionAsync(id, request, User.GetUserId(), ActorName, ct);
            return Ok(action);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPut("{id:int}/actions/{actionId:int}")]
    public async Task<IActionResult> UpdateAction(int id, int actionId, [FromBody] UpdateExecutionActionRequest request, CancellationToken ct)
    {
        // التعديل للمحامي فقط
        if (!CanManageActions)
            return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        try
        {
            var action = await _documents.UpdateExecutionActionAsync(id, actionId, request, ActorName, ct);
            if (action is null) return NotFound();
            return Ok(action);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpDelete("{id:int}/actions/{actionId:int}")]
    public async Task<IActionResult> DeleteAction(int id, int actionId, CancellationToken ct)
    {
        // الحذف للمحامي فقط
        if (!CanManageActions)
            return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        var deleted = await _documents.DeleteExecutionActionAsync(id, actionId, ActorName, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id:int}/actions/{actionId:int}/reminder")]
    public async Task<IActionResult> ClearReminder(int id, int actionId, CancellationToken ct)
    {
        // إلغاء التذكير للمحامي فقط
        if (!CanManageActions)
            return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        return await _documents.ClearReminderAsync(id, actionId, ActorName, ct)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{id:int}/occurrences")]
    public async Task<IActionResult> AddOccurrence(int id, [FromBody] UpsertOccurrenceRequest request, CancellationToken ct)
    {
        // إدارة وقوعات الملف للمحامي صاحب الملف فقط.
        if (!CanEdit) return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        try
        {
            var occurrence = await _documents.AddOccurrenceAsync(id, request, User.GetUserId(), ActorName, ct);
            return Ok(occurrence);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPut("{id:int}/occurrences/{occurrenceId:int}")]
    public async Task<IActionResult> UpdateOccurrence(int id, int occurrenceId, [FromBody] UpsertOccurrenceRequest request, CancellationToken ct)
    {
        // إدارة وقوعات الملف للمحامي صاحب الملف فقط.
        if (!CanEdit) return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        try
        {
            var occurrence = await _documents.UpdateOccurrenceAsync(id, occurrenceId, request, ActorName, ct);
            if (occurrence is null) return NotFound();
            return Ok(occurrence);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpDelete("{id:int}/occurrences/{occurrenceId:int}")]
    public async Task<IActionResult> DeleteOccurrence(int id, int occurrenceId, CancellationToken ct)
    {
        // إدارة وقوعات الملف للمحامي صاحب الملف فقط.
        if (!CanEdit) return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!CanAccess(doc)) return Forbid();

        return await _documents.DeleteOccurrenceAsync(id, occurrenceId, ActorName, ct)
            ? NoContent()
            : NotFound();
    }

    public class StatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, string?>? Fields { get; set; }
    }

    public class ExecutedStatusRequest : RenewalRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}
