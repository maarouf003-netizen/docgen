using DocGenerator.Api.Authorization;
using DocGenerator.Api.Security;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "lawyer,head,subhead,manager,admin")]
public class DocumentsController : ControllerBase
{
    private const string WordContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IDocumentService _documents;
    private readonly IWordDocumentGenerator _generator;
    private readonly IExcelExportService _excel;
    private readonly IDocumentAppealService _appeals;
    private readonly IAuditLogService _auditLogs;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IExecutionCircuitService? _circuits;

    public DocumentsController(
        IDocumentService documents,
        IWordDocumentGenerator generator,
        IExcelExportService excel,
        IDocumentAppealService appeals,
        IAuditLogService auditLogs,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IExecutionCircuitService? circuits = null)
    {
        _documents = documents;
        _generator = generator;
        _excel = excel;
        _appeals = appeals;
        _auditLogs = auditLogs;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
        _circuits = circuits;
    }

    private string? ActorName => User.Identity?.Name;

    private UserRole Role => User.GetRoleEnum();
    private bool HasFullAccess => RolePermissions.HasFullAccess(Role);
    private bool IsHeadOrSubHead => RolePermissions.IsHeadOrSubHead(Role);
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

    /// <summary>
    /// استجابة قائمة مع تعقيم العدادات لغير المصرَّح لهم (سياسة CanViewCounters) —
    /// المصدر الوحيد لهذا النمط في نقاط القوائم الخمس (المسار (ب) من الخطة).
    /// </summary>
    private IActionResult OkSanitized(PagedResult<DocumentResponse> result)
    {
        if (!CanViewCounters)
            result.Items = result.Items.Select(Sanitize).ToList();
        return Ok(result);
    }

    /// <summary>
    /// نطاق المالك المشتق خادميًا لقوائم الملفات (§5 — قرار §2.21): شعبة رئيس
    /// الشعبة من الرمز، و`null` لغيره. رئيس شعبة برمز بلا شعبة مرفوض (أعد
    /// الدخول) بدل التدهور لنطاق القسم.
    /// </summary>
    private ActionResult? RequireOwnerScope(out int? ownerSectionId)
    {
        ownerSectionId = null;
        if (Role == UserRole.SubHead)
        {
            ownerSectionId = User.GetSectionId();
            if (ownerSectionId is null)
                return Forbid();
        }
        return null;
    }

    /// <summary>
    /// وصول القراءة بالنطاق (§5 + الاستثناء القرائي 22′): مدير/مشرف (الكل)؛
    /// محامٍ (ملفاته)؛ رئيس قسم (ملفات دوائر القسم + بلا دائرة + المحال له حتى
    /// الحسم)؛ رئيس شعبة (ملفات دوائر شعبته).
    /// </summary>
    private async Task<bool> CanAccessAsync(DocumentResponse doc)
    {
        if (HasFullAccess) return true;
        if (Role == UserRole.Lawyer)
            return doc.CreatedById == User.GetUserId();
        if (doc.BranchId != User.GetBranchId())
            return false;
        if (Role == UserRole.Head)
        {
            if (doc.ExecutionCircuitId is null || !doc.SectionId.HasValue)
                return true;
            // الاستثناء القرائي: محال لرئيس قسم نفس الفرع — يُرى حتى الحسم.
            return await _appeals.HasForwardedAppealAsync(doc.Id);
        }
        if (Role == UserRole.SubHead)
            return doc.SectionId.HasValue && doc.SectionId == User.GetSectionId();
        return false;
    }

    /// <summary>
    /// وصول القراءة الموسّع: يضيف على قاعدة CanAccess المحامي المُسند إليه متابعة
    /// استئناف على هذا الملف (قراءة فقط — لصفحة تفاصيل الاستئناف).
    /// </summary>
    private async Task<bool> CanAccessOrFollowAsync(DocumentResponse doc)
        => await CanAccessAsync(doc) || await _appeals.IsAssignedFollowerAsync(doc.Id, User.GetUserId());

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] string? applicant, [FromQuery] string? court,
        [FromQuery] string? lawyer, [FromQuery] string? branch,
        [FromQuery] string? administrativeBranch, [FromQuery] string? executedEntity,
        [FromQuery] string? publicEntityBranch,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // البحث/الفلترة باسم المحامي محصور برئيس القسم والشعبة/المدير/المشرف.
        if (!string.IsNullOrWhiteSpace(lawyer) && !CanSearchByLawyer)
            return Forbid();

        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = HasFullAccess || IsHeadOrSubHead ? (int?)null : User.GetUserId();
        var scopeError = RequireOwnerScope(out var ownerSectionId);
        if (scopeError is not null) return scopeError;

        var result = await _documents.SearchAsync(q, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch,
            page, perPage, visibleBranch, visibleUser, ct, ownerSectionId: ownerSectionId);
        return OkSanitized(result);
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
        var visibleUser = HasFullAccess || IsHeadOrSubHead ? (int?)null : User.GetUserId();
        var scopeError = RequireOwnerScope(out var ownerSectionId);
        if (scopeError is not null) return scopeError;
        var options = await _documents.GetFilterOptionsAsync(status, applicant, court, lawyer, branch,
            administrativeBranch, executedEntity, publicEntityBranch, visibleBranch, visibleUser, ct, ownerSectionId: ownerSectionId);
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
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    public async Task<IActionResult> Export(
        [FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] string? applicant, [FromQuery] string? court,
        [FromQuery] string? lawyer, [FromQuery] string? branch,
        [FromQuery] string? administrativeBranch, [FromQuery] string? executedEntity,
        [FromQuery] string? publicEntityBranch, CancellationToken ct)
    {
        // التصدير يحترم نفس أذونات الفلترة: البحث باسم المحامي محصور برئيس القسم والشعبة/المدير/المشرف.
        if (!string.IsNullOrWhiteSpace(lawyer) && !CanSearchByLawyer)
            return Forbid();

        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = HasFullAccess || IsHeadOrSubHead ? (int?)null : User.GetUserId();
        var scopeError = RequireOwnerScope(out var ownerSectionId);
        if (scopeError is not null) return scopeError;

        var items = await _documents.ExportAsync(q, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch,
            visibleBranch, visibleUser, ct, ActorName, ownerSectionId: ownerSectionId);
        // بلا Sanitize هنا: عمود «عدد المشاهدات» محكوم أصلًا براية includeViewCount
        // (= CanViewCounters) فلا يظهر لغير المصرَّح لهم إطلاقًا — والورقة متطابقة.

        var bytes = _excel.BuildDocumentsWorkbook(
            items,
            includeAdministrativeBranch: RolePermissions.CanSeeAdministrativeBranch(Role),
            includeAssignedLawyer: RolePermissions.CanSeeAssignedLawyer(Role),
            includeViewCount: CanViewCounters);

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"الملفات التنفيذية {ServerClock.TodayString(_clock, _timeZone, "yyyy-MM-dd")}.xlsx");
    }

    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeleted(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // رؤية المحذوفات: محامٍ (ملفاته) / رئيس قسم (فرعه) / مدير ومشرف (الكل) — `BQ-001`.
        if (!RolePermissions.CanViewDeletedDocuments(Role))
            return Forbid();

        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = HasFullAccess || IsHeadOrSubHead ? (int?)null : User.GetUserId();
        var scopeError = RequireOwnerScope(out var ownerSectionId);
        if (scopeError is not null) return scopeError;

        var result = await _documents.SearchDeletedAsync(q, page, perPage, visibleBranch, visibleUser, ct, ownerSectionId);
        return OkSanitized(result);
    }

    [HttpGet("struck-off")]
    public async Task<IActionResult> GetStruckOff(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // رؤية الملفات المشطوبة في وضع «منفذ عليه» بنفس صلاحيات المحذوفات:
        // محامٍ (ملفاته) / رئيس قسم (فرعه) / مدير ومشرف (الكل) — `BQ-001`.
        if (!RolePermissions.CanViewDeletedDocuments(Role))
            return Forbid();

        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = HasFullAccess || IsHeadOrSubHead ? (int?)null : User.GetUserId();
        var scopeError = RequireOwnerScope(out var ownerSectionId);
        if (scopeError is not null) return scopeError;

        var result = await _documents.SearchStruckOffAsync(q, page, perPage, visibleBranch, visibleUser, ct, ownerSectionId);
        return OkSanitized(result);
    }

    [HttpGet("executed")]
    public async Task<IActionResult> GetExecuted(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // صفحة «الملفات المنفذة» ظاهرة لجميع الأدوار (لا تُحجب بصلاحية المحذوفات):
        // محامٍ (ملفاته) / رئيس قسم (فرعه) / ذو الوصول الكامل (الكل).
        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = HasFullAccess || IsHeadOrSubHead ? (int?)null : User.GetUserId();
        var scopeError = RequireOwnerScope(out var ownerSectionId);
        if (scopeError is not null) return scopeError;

        var result = await _documents.SearchExecutedAsync(q, page, perPage, visibleBranch, visibleUser, ct, ownerSectionId);
        return OkSanitized(result);
    }

    [HttpGet("referred-to-start")]
    public async Task<IActionResult> GetReferredToStart(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // صفحة «محال الى البداية» ظاهرة لجميع الأدوار (كصفحة «الملفات المنفذة»):
        // محامٍ (ملفاته) / رئيس قسم (فرعه) / ذو الوصول الكامل (الكل).
        var visibleBranch = HasFullAccess ? (int?)null : User.GetBranchId();
        var visibleUser = HasFullAccess || IsHeadOrSubHead ? (int?)null : User.GetUserId();
        var scopeError = RequireOwnerScope(out var ownerSectionId);
        if (scopeError is not null) return scopeError;

        var result = await _documents.SearchReferredToStartAsync(q, page, perPage, visibleBranch, visibleUser, ct, ownerSectionId);
        return OkSanitized(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!await CanAccessOrFollowAsync(doc)) return Forbid();
        // RF-018 (SEC-003 + قرار BQ-021): فتح التفاصيل الناجح والمصرّح يُدوَّن (قارئ + ملف) —
        // في المتحكم عمدًا لا الخدمة (GetAsync تُستدعَى من مسارات أخرى)، والقوائم خارج النطاق.
        await _audit.LogAsync(ActorName, "view_document", doc.Id, doc.DocumentType,
            $"اطّلع على الملف (رقم {doc.Id})", ct);
        return Ok(Sanitize(doc));
    }

    [HttpGet("{id:int}/base-numbers")]
    public async Task<IActionResult> GetBaseNumberHistory(int id, CancellationToken ct)
    {
        // تاريخ أرقام الأساس — بنفس صلاحيات العرض المفصّل للملف.
        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!await CanAccessOrFollowAsync(doc)) return Forbid();
        return Ok(await _documents.GetBaseNumberHistoryAsync(id, ct));
    }

    /// <summary>
    /// سجل تعديلات الملف على مستوى الحقول (قبل/بعد) — لصاحب الملف ورئيس قسمه
    /// والمدير/المشرف. أداة المراجعة المؤسسية؛ لا تُفتح لمتابعي الإنابة/الاستئناف.
    /// </summary>
    [HttpGet("{id:int}/changes")]
    public async Task<IActionResult> GetChanges(
        int id, [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!await CanAccessAsync(doc)) return Forbid();
        return Ok(await _auditLogs.GetDocumentChangesAsync(id, page, perPage, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DocumentUpsertRequest request, CancellationToken ct)
    {
        // إدخال الملفات محصور بالمحامي: لا إدخال للرؤساء، وقراءة مطلقة للمدير/المشرف.
        if (!CanEdit)
            return Forbid();

        try
        {
            // RF-011: مفتاح عدم التكرار ترويسة اختيارية — التكرار يُردّ `200` بالجسم المخزن نفسه.
            var doc = await _documents.CreateAsync(request, User.GetUserId(), ActorName, User.GetBranchId(), ct,
                IdempotencyKeyHeader());
            return CreatedAtAction(nameof(Get), new { id = doc.Id }, Sanitize(doc));
        }
        catch (IdempotentReplayException r)
        {
            return Content(r.ResponseBody, "application/json");
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
        if (!await CanAccessAsync(doc)) return Forbid();

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
        if (!await CanAccessAsync(doc)) return Forbid();

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
        if (!await CanAccessAsync(doc)) return Forbid();

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
        if (!await CanAccessAsync(doc)) return Forbid();

        try
        {
            var ok = await _documents.UpdateStatusAsync(id, request.Status, request.Fields ?? new(), ActorName, ct, request.Version);
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
        if (!await CanAccessAsync(doc)) return Forbid();

        try
        {
            var ok = await _documents.RevertStatusAsync(id, request.Fields ?? new(), ActorName, ct, request.Version);
            return ok ? Ok(new { message = "عُد الملف إلى المتداول" }) : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost("{id:int}/return-referred-to-start")]
    public async Task<IActionResult> ReturnFromReferredToStart(int id, [FromBody] ReturnReferredToStartRequest request, CancellationToken ct)
    {
        // العودة من «محال الى البداية» (بنتيجتين: استعادة «منفذ جزئيًا» حسب اللازمة وإلا
        // «متداول») محصورة بالمحامي (للملفات التي يملكها) — بصلاحية «تغيير الحالة» كنظيريها
        // التراجع/الاعتبار، وبلا حقول إلزامية (رقم الملف الجديد الاختياري يفعّل التجديد).
        if (!CanChangeStatus) return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!await CanAccessAsync(doc)) return Forbid();

        try
        {
            var ok = await _documents.ReturnFromReferredToStartAsync(id, request ?? new(), ActorName, ct);
            return ok ? Ok(new { message = "أعيد السير بالملف" }) : NotFound();
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
    }

    [HttpPost("{id:int}/consider-executed-by-delegation")]
    public async Task<IActionResult> ConsiderExecutedByDelegation(int id, [FromBody] StatusRequest request, CancellationToken ct)
    {
        // «اعتبار الملف منفذًا كاملًا بهذا البيع» (إغلاق «منفذ جبريا — منفذ جزئيا» الذي
        // فُعّل تلقائيًا بإتمام إنابة) محصور بالمحامي (للملفات التي يملكها) — يُلزم إدخال
        // «تاريخ تحويل بدل المبيع للجهة العامة» مع «رقم الإشعار» الاختياري.
        if (!CanChangeStatus) return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!await CanAccessAsync(doc)) return Forbid();

        try
        {
            var ok = await _documents.ConsiderExecutedByDelegationAsync(id, request.Fields ?? new(), ActorName, ct, request.Version);
            return ok ? Ok(new { message = "اعتُبر الملف منفذًا كاملًا بهذا البيع" }) : NotFound();
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
        if (!await CanAccessAsync(doc)) return Forbid();

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
        if (!await CanAccessAsync(doc)) return Forbid();

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
        if (!await CanAccessAsync(doc)) return Forbid();
        await _documents.IncrementViewCountAsync(id, ct);
        return Ok();
    }

    [HttpGet("{id:int}/generate")]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
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
        if (!await CanAccessAsync(doc)) return Forbid();

        // توليد المستندات محصور بنظام «طالبة تنفيذ»: ملفات عائلة وضع «منفذ عليه» لا تُولَّد.
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            return BadRequest(new { message = "لا يُولَّد مستند لملفات وضع «الجهة العامة منفذ عليها» و«عرض وايداع»" });

        try
        {
            var result = await _generator.GenerateAsync(id, template, recipient, estateIds, heirId, ct, ActorName);
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
        // عدد ملفات المحامي (معاينة قبل النقل الجماعي) — رئيس القسم والشعبة
        // (ضمن فرعه) فقط — بالنطاق نفسه (§5.5): المعاينة تطابق المنقول فعلًا.
        if (!RolePermissions.CanTransferDocuments(Role))
            return Forbid();

        // رئيس القسم والشعبة بلا فرع لا يملك نطاقًا صالحًا للنقل (يُمنع صراحةً كبقية عمليات الفرع).
        var scopeBranchId = User.GetBranchId();
        if (scopeBranchId is null)
            return Forbid();
        var scopeError = RequireOwnerScope(out var ownerSectionId);
        if (scopeError is not null) return scopeError;

        try
        {
            var count = await _documents.CountFilesByOwnerAsync(lawyerId, scopeBranchId, ct, ownerSectionId);
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
        // نقل كامل ملفات محامٍ إلى محامٍ آخر بجميع الحالات — رئيس القسم والشعبة
        // (ضمن فرعه) فقط — بتقاطع النطاق (§5.5): ملفات المصدر ضمن نطاق المنفِّذ فقط.
        if (!RolePermissions.CanTransferDocuments(Role))
            return Forbid();

        // رئيس القسم والشعبة بلا فرع لا يملك نطاقًا صالحًا للنقل (يُمنع صراحةً كبقية عمليات الفرع).
        var scopeBranchId = User.GetBranchId();
        if (scopeBranchId is null)
            return Forbid();
        var scopeError = RequireOwnerScope(out var ownerSectionId);
        if (scopeError is not null) return scopeError;

        try
        {
            // RF-011: التكرار بنفس المفتاح يُعيد العدد المخزن نفسه.
            var transferredCount = await _documents.TransferAllAsync(
                request.SourceLawyerId, request.TargetLawyerId, scopeBranchId, ActorName, ct,
                IdempotencyKeyHeader(), ownerSectionId);
            // سجل الدوائر (H2): التنبيه يتبع المالك — مزامنة تنبيهات إعادة القيد بعد
            // النقل (إشعار فرعي: فشلها يُسجَّل ولا يُفشل النقل الناجح).
            await SyncCircuitAlertsAfterTransferAsync(
                request.SourceLawyerId, request.TargetLawyerId, scopeBranchId.Value, ct);
            return Ok(new { transferredCount });
        }
        catch (IdempotentReplayException r)
        {
            return Content(r.ResponseBody, "application/json");
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
        // نقل الملفات بين المحامين — رئيس القسم والشعبة (ضمن فرعه) فقط —
        // والملف نفسه ضمن نطاق المنفِّذ (`CanAccessAsync`).
        if (!RolePermissions.CanTransferDocuments(Role))
            return Forbid();

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        if (!await CanAccessAsync(doc)) return Forbid();

        try
        {
            var updated = await _documents.TransferAsync(id, request.TargetLawyerId, ActorName, ct);
            // سجل الدوائر (H2): التنبيه يتبع المالك (إشعار فرعي — انظر أعلاه).
            await SyncCircuitAlertsAfterTransferAsync(
                doc.CreatedById, request.TargetLawyerId, doc.BranchId ?? User.GetBranchId() ?? 0, ct);
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
        if (!await CanAccessOrFollowAsync(doc)) return Forbid();

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
        if (!await CanAccessAsync(doc)) return Forbid();

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
        if (!await CanAccessAsync(doc)) return Forbid();

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
        if (!await CanAccessAsync(doc)) return Forbid();

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
        if (!await CanAccessAsync(doc)) return Forbid();

        return await _documents.ClearReminderAsync(id, actionId, ActorName, ct)
            ? NoContent()
            : NotFound();
    }

    public class StatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, string?>? Fields { get; set; }
        /// <summary>عدّاد التزامن المتفائل (RF-010) — غيابه = قبول بلا فحص مبكر.</summary>
        public long? Version { get; set; }
    }

    /// <summary>مفتاح عدم التكرار (RF-011) من الترويسة — غيابه = المسار القديم.</summary>
    private string? IdempotencyKeyHeader() =>
        Request.Headers.TryGetValue(IdempotencyGuard.HeaderName, out var values)
            ? values.ToString()
            : null;

    /// <summary>
    /// سجل الدوائر (H2): مزامنة تنبيهات إعادة القيد بعد نقل ملكية ناجح — إشعار فرعي
    /// (أفضل جهد): أي فشل يُسجَّل في التدقيق ولا يُفشل النقل الذي تمّ فعلًا.
    /// </summary>
    private async Task SyncCircuitAlertsAfterTransferAsync(
        int sourceOwnerId, int targetOwnerId, int branchId, CancellationToken ct)
    {
        if (_circuits is null)
            return;
        try
        {
            await _circuits.SyncPendingAlertsAfterTransferAsync(
                sourceOwnerId, targetOwnerId, branchId, User.GetUserId(), ct);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(ActorName, "head_alert_failed",
                details: $"تعذّر مزامنة تنبيهات إعادة القيد بعد نقل الملكية: {ex.Message}", ct: ct);
        }
    }
}
