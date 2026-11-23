using DocGenerator.Api.Authorization;
using DocGenerator.Api.Authorization.Documents;
using DocGenerator.Api.Filters;
using DocGenerator.Api.Http;
using DocGenerator.Api.Security;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Options;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Documents;
using DocGenerator.Application.UseCases.ExecutionActions;
using DocGenerator.Application.UseCases.Interventions;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "lawyer,head,subhead,manager,admin")]
public class DocumentsController : ControllerBase
{
    private const string WordContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>
    /// رسالة `404` الموحّدة لعمليات الحالة التي تسبقها قراءة ناجحة للمستند ثم يرفضها
    /// التنفيذ (سباق حذف/تغيّر حالة). الشكل `{ message }` هو عقد الواجهة
    /// (<c>frontend/src/api/client.ts</c>) بدل جسم فارغ لا يستطيع عرض سبب.
    /// </summary>
    private const string DocumentNotFoundMessage = "المستند غير موجود";

    private readonly IDocumentService _documents;
    private readonly IExecutionActionService _executionActions;
    private readonly IDocumentSearchService _search;
    private readonly IDocumentStatusService _status;
    private readonly IWordDocumentGenerator _generator;
    private readonly IOptions<DocumentGenerationOptions> _generationOptions;
    private readonly IExcelExportService _excel;
    private readonly IDocumentAppealService _appeals;
    private readonly IStrikeOffReminderService _strikeOffReminders;
    private readonly IAuditLogService _auditLogs;
    private readonly IAuditLogger _audit;
    private readonly IDocumentAuthorization _authorization;
    private readonly IQueryHandler<GetDocumentQuery, DocumentResponse?> _getDocument;
    private readonly ICommandHandler<CreateDocumentCommand, DocumentResponse> _createDocument;
    private readonly ICommandHandler<UpdateDocumentCommand, DocumentResponse?> _updateDocument;
    private readonly ICommandHandler<DismissStrikeOffReminderCommand, bool> _dismissStrikeOffReminder;
    private readonly ICommandHandler<DeleteExecutionActionCommand, bool> _deleteExecutionAction;
    private readonly ICommandHandler<DeleteDocumentInterventionCommand, bool> _deleteIntervention;
    private readonly ICommandHandler<AddDocumentInterventionCommand, DocumentInterventionDto> _addIntervention;
    private readonly ICommandHandler<UpdateDocumentInterventionCommand, DocumentInterventionDto?> _updateIntervention;
    private readonly ICommandHandler<AddExecutionActionCommand, ExecutionActionDto> _addExecutionAction;
    private readonly ICommandHandler<UpdateExecutionActionCommand, ExecutionActionDto?> _updateExecutionAction;
    private readonly ICommandHandler<ClearActionReminderCommand, bool> _clearReminder;
    private readonly ICommandHandler<SetActionReminderCommand, ExecutionActionDto> _setActionReminder;
    private readonly ICommandHandler<RescheduleActionReminderCommand, ExecutionActionDto> _rescheduleActionReminder;
    private readonly ICommandHandler<RestoreDocumentCommand, bool> _restoreDocument;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public DocumentsController(
        IDocumentService documents,
        IExecutionActionService executionActions,
        IDocumentSearchService search,
        IDocumentStatusService status,
        IWordDocumentGenerator generator,
        IOptions<DocumentGenerationOptions> generationOptions,
        IExcelExportService excel,
        IDocumentAppealService appeals,
        IStrikeOffReminderService strikeOffReminders,
        IAuditLogService auditLogs,
        IAuditLogger audit,
        IDocumentAuthorization authorization,
        IQueryHandler<GetDocumentQuery, DocumentResponse?> getDocument,
        ICommandHandler<CreateDocumentCommand, DocumentResponse> createDocument,
        ICommandHandler<UpdateDocumentCommand, DocumentResponse?> updateDocument,
        ICommandHandler<DismissStrikeOffReminderCommand, bool> dismissStrikeOffReminder,
        ICommandHandler<DeleteExecutionActionCommand, bool> deleteExecutionAction,
        ICommandHandler<DeleteDocumentInterventionCommand, bool> deleteIntervention,
        ICommandHandler<AddDocumentInterventionCommand, DocumentInterventionDto> addIntervention,
        ICommandHandler<UpdateDocumentInterventionCommand, DocumentInterventionDto?> updateIntervention,
        ICommandHandler<AddExecutionActionCommand, ExecutionActionDto> addExecutionAction,
        ICommandHandler<UpdateExecutionActionCommand, ExecutionActionDto?> updateExecutionAction,
        ICommandHandler<ClearActionReminderCommand, bool> clearReminder,
        ICommandHandler<SetActionReminderCommand, ExecutionActionDto> setActionReminder,
        ICommandHandler<RescheduleActionReminderCommand, ExecutionActionDto> rescheduleActionReminder,
        ICommandHandler<RestoreDocumentCommand, bool> restoreDocument,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _documents = documents;
        _executionActions = executionActions;
        _search = search;
        _status = status;
        _generator = generator;
        _generationOptions = generationOptions;
        _excel = excel;
        _appeals = appeals;
        _strikeOffReminders = strikeOffReminders;
        _auditLogs = auditLogs;
        _audit = audit;
        _authorization = authorization;
        _getDocument = getDocument;
        _createDocument = createDocument;
        _updateDocument = updateDocument;
        _dismissStrikeOffReminder = dismissStrikeOffReminder;
        _deleteExecutionAction = deleteExecutionAction;
        _deleteIntervention = deleteIntervention;
        _addIntervention = addIntervention;
        _updateIntervention = updateIntervention;
        _addExecutionAction = addExecutionAction;
        _updateExecutionAction = updateExecutionAction;
        _clearReminder = clearReminder;
        _setActionReminder = setActionReminder;
        _rescheduleActionReminder = rescheduleActionReminder;
        _restoreDocument = restoreDocument;
        _clock = clock;
        _timeZone = timeZone;
    }

    private string? ActorName => User.Identity?.Name;

    /// <summary>
    /// تعقيم عدّاد الطباعة لغير المصرَّح لهم (`CanViewPrintCounter`) — قدرة دورّية غير
    /// موردية تُقرأ من الكتالوج عبر <see cref="IDocumentAuthorization"/>، لا قاعدة محلية.
    /// </summary>
    private bool CanViewPrintCounter => _authorization.Has(User, UserCapability.CanViewPrintCounter);

    private DocumentResponse Sanitize(DocumentResponse doc)
    {
        if (CanViewPrintCounter) return doc;
        doc.PrintCount = 0;
        return doc;
    }

    private ActionResult<PagedResult<DocumentResponse>> OkSanitized(PagedResult<DocumentResponse> result)
    {
        if (!CanViewPrintCounter)
            result.Items = result.Items.Select(Sanitize).ToList();
        return Ok(result);
    }

    /// <summary>
    /// ترجمة نتيجة سياسة الوصول إلى استجابة: `null` = متابعة، وإلا `404` (مفقود حُلّ
    /// قبل التقييم) أو `403` (موجود خارج النطاق/القدرة). المورد يُحلّ داخل السياسة
    /// بإسقاط F-01 (قراءة واحدة بلا شجرة Include)، فلا يحمل المتحكم أي قاعدة وصول.
    /// </summary>
    private async Task<ActionResult?> RequireDocumentAccessAsync(
        int id, DocumentAction action, CancellationToken ct)
    {
        var outcome = await _authorization.AuthorizeAsync(User, id, action, ct);
        return outcome switch
        {
            DocumentAccessOutcome.Allowed => null,
            DocumentAccessOutcome.NotFound => NotFound(),
            _ => Forbid(),
        };
    }

    /// <summary>
    /// تقييم مورد محمَّل مسبقًا (تفاصيل/ملف محذوف) بلا قراءة إضافية — المفقود يُفحص
    /// عند التحميل، فالفشل هنا `403` فقط.
    /// </summary>
    private async Task<ActionResult?> RequireDocumentAccessAsync(
        DocumentResponse doc, DocumentAction action, CancellationToken ct)
        => await _authorization.AuthorizeAsync(User, doc, action, ct) == DocumentAccessOutcome.Allowed
            ? null
            : Forbid();

    [HttpGet]
    public async Task<ActionResult<PagedResult<DocumentResponse>>> Search(
        [FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] string? applicant, [FromQuery] string? court,
        [FromQuery] string? lawyer, [FromQuery] string? branch,
        [FromQuery] string? administrativeBranch, [FromQuery] string? executedEntity,
        [FromQuery] string? publicEntityBranch,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // البحث/الفلترة باسم المحامي محصور برئيس القسم والشعبة/المدير/المشرف.
        if (!string.IsNullOrWhiteSpace(lawyer)
            && !_authorization.Has(User, UserCapability.CanSearchByLawyer))
            return Forbid();

        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var result = await _search.SearchAsync(q, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch,
            page, perPage, scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId);
        return OkSanitized(result);
    }

    /// <summary>
    /// قائمة الملفات بإسقاط قراءة (P3-04b): نفس فلاتر/صلاحيات `Search`، لكن عناصر
    /// `DocumentListItemDto` المصغّرة بدل `DocumentResponse` الكامل — مسار إضافي
    /// لا يغيّر العقد القائم.
    /// </summary>
    [HttpGet("list")]
    public async Task<ActionResult<PagedResult<DocumentListItemDto>>> List(
        [FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] string? applicant, [FromQuery] string? court,
        [FromQuery] string? lawyer, [FromQuery] string? branch,
        [FromQuery] string? administrativeBranch, [FromQuery] string? executedEntity,
        [FromQuery] string? publicEntityBranch,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        [FromQuery] string? cursor = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(lawyer)
            && !_authorization.Has(User, UserCapability.CanSearchByLawyer))
            return Forbid();

        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var result = await _search.SearchListAsync(q, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch,
            page, perPage, scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId, cursor: cursor);
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
        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();
        var options = await _search.GetFilterOptionsAsync(status, applicant, court, lawyer, branch,
            administrativeBranch, executedEntity, publicEntityBranch,
            scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId);
        return Ok(new
        {
            applicants = options.Applicants,
            courts = options.Courts,
            lawyers = _authorization.Has(User, UserCapability.CanSearchByLawyer)
                ? options.Lawyers
                : new List<string>(),
            administrativeBranches = options.AdministrativeBranches,
            branches = options.Branches,
            executedEntities = options.ExecutedEntities,
            publicEntityBranches = options.PublicEntityBranches,
        });
    }

    [HttpGet("export")]
    [EnableRateLimiting(RateLimitingSetup.ExpensivePolicy)]
    [SingleExportPerUser]
    public async Task<IActionResult> Export(
        [FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] string? applicant, [FromQuery] string? court,
        [FromQuery] string? lawyer, [FromQuery] string? branch,
        [FromQuery] string? administrativeBranch, [FromQuery] string? executedEntity,
        [FromQuery] string? publicEntityBranch, CancellationToken ct)
    {
        // التصدير يحترم نفس أذونات الفلترة: البحث باسم المحامي محصور برئيس القسم والشعبة/المدير/المشرف.
        if (!string.IsNullOrWhiteSpace(lawyer)
            && !_authorization.Has(User, UserCapability.CanSearchByLawyer))
            return Forbid();

        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var items = await _search.ExportAsync(q, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch,
            scope.VisibleBranchId, scope.VisibleUserId, ct, ActorName, ownerSectionId: scope.OwnerSectionId);
        // بلا Sanitize هنا: ورقة التصدير لا تحمل أي عدّاد (لا مشاهدة ولا طباعة) — فلا شيء يُعقَّم.

        var stream = _excel.BuildDocumentsWorkbook(
            items,
            includeAdministrativeBranch: _authorization.Has(User, UserCapability.CanSeeAdministrativeBranch),
            includeAssignedLawyer: _authorization.Has(User, UserCapability.CanSeeAssignedLawyer));

        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"الملفات التنفيذية {ServerClock.TodayString(_clock, _timeZone, "yyyy-MM-dd")}.xlsx");
    }

    [HttpGet("deleted")]
    public async Task<ActionResult<PagedResult<DocumentResponse>>> GetDeleted(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // رؤية المحذوفات: محامٍ (ملفاته) / رئيس قسم (فرعه) / مدير ومشرف (الكل) — `BQ-001`.
        if (!_authorization.Has(User, UserCapability.CanViewDeletedDocuments))
            return Forbid();

        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var result = await _search.SearchDeletedAsync(q, page, perPage, scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId);
        return OkSanitized(result);
    }

    /// <summary>
    /// قائمة المحذوفات بإسقاط قراءة (P3-04c): نفس الصلاحية/النطاق/الفلترة والترتيب
    /// والترقيم، لكن عناصر `DocumentListItemDto` — مسار إضافي لا يغيّر العقد القائم.
    /// </summary>
    [HttpGet("deleted/list")]
    public async Task<ActionResult<PagedResult<DocumentListItemDto>>> GetDeletedList(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        [FromQuery] string? cursor = null, CancellationToken ct = default)
    {
        if (!_authorization.Has(User, UserCapability.CanViewDeletedDocuments))
            return Forbid();

        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var result = await _search.SearchDeletedListAsync(q, page, perPage, scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId, cursor: cursor);
        return Ok(result);
    }

    [HttpGet("struck-off")]
    public async Task<ActionResult<PagedResult<DocumentResponse>>> GetStruckOff(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // رؤية الملفات المشطوبة في وضع «منفذ عليه» بنفس صلاحيات المحذوفات:
        // محامٍ (ملفاته) / رئيس قسم (فرعه) / مدير ومشرف (الكل) — `BQ-001`.
        if (!_authorization.Has(User, UserCapability.CanViewDeletedDocuments))
            return Forbid();

        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var result = await _search.SearchStruckOffAsync(q, page, perPage, scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId);
        return OkSanitized(result);
    }

    /// <summary>قائمة المشطوبة بإسقاط قراءة (P3-04c) — نفس الصلاحية/النطاق/الترتيب حرفيًا.</summary>
    [HttpGet("struck-off/list")]
    public async Task<ActionResult<PagedResult<DocumentListItemDto>>> GetStruckOffList(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        [FromQuery] string? cursor = null, CancellationToken ct = default)
    {
        if (!_authorization.Has(User, UserCapability.CanViewDeletedDocuments))
            return Forbid();

        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var result = await _search.SearchStruckOffListAsync(q, page, perPage, scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId, cursor: cursor);
        return Ok(result);
    }

    [HttpGet("executed")]
    public async Task<ActionResult<PagedResult<DocumentResponse>>> GetExecuted(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // صفحة «الملفات المنفذة» ظاهرة لجميع الأدوار (لا تُحجب بصلاحية المحذوفات):
        // محامٍ (ملفاته) / رئيس قسم (فرعه) / ذو الوصول الكامل (الكل).
        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var result = await _search.SearchExecutedAsync(q, page, perPage, scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId);
        return OkSanitized(result);
    }

    /// <summary>قائمة المنفذة بإسقاط قراءة (P3-04c) — نفس الصلاحية/النطاق/الترتيب حرفيًا.</summary>
    [HttpGet("executed/list")]
    public async Task<ActionResult<PagedResult<DocumentListItemDto>>> GetExecutedList(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        [FromQuery] string? cursor = null, CancellationToken ct = default)
    {
        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var result = await _search.SearchExecutedListAsync(q, page, perPage, scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId, cursor: cursor);
        return Ok(result);
    }

    [HttpGet("referred-to-start")]
    public async Task<ActionResult<PagedResult<DocumentResponse>>> GetReferredToStart(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // صفحة «محال الى البداية» ظاهرة لجميع الأدوار (كصفحة «الملفات المنفذة»):
        // محامٍ (ملفاته) / رئيس قسم (فرعه) / ذو الوصول الكامل (الكل).
        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var result = await _search.SearchReferredToStartAsync(q, page, perPage, scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId);
        return OkSanitized(result);
    }

    /// <summary>قائمة «محال الى البداية» بإسقاط قراءة (P3-04c) — نفس الصلاحية/النطاق/الترتيب حرفيًا.</summary>
    [HttpGet("referred-to-start/list")]
    public async Task<ActionResult<PagedResult<DocumentListItemDto>>> GetReferredToStartList(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        [FromQuery] string? cursor = null, CancellationToken ct = default)
    {
        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var result = await _search.SearchReferredToStartListAsync(q, page, perPage, scope.VisibleBranchId, scope.VisibleUserId, ct, ownerSectionId: scope.OwnerSectionId, cursor: cursor);
        return Ok(result);
    }

    /// <summary>
    /// بطاقة «تذكيرات الشطب»: ملفات «طالبة تنفيذ» النشطة التي مضى على آخر إجراء فيها 5 أشهر —
    /// المحامي فقط ولملفاته (النطاق من الرمز). القراءة محسوبة عند الطلب بلا كتابة.
    /// </summary>
    [HttpGet("strike-off-reminders")]
    public async Task<ActionResult<PagedResult<DocumentResponse>>> GetStrikeOffReminders(
        [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        if (!_authorization.Has(User, UserCapability.CanViewStrikeOffReminders))
            return Forbid();

        var result = await _strikeOffReminders.GetAsync(
            page, perPage, User.GetBranchId(), User.GetUserId(), ownerSectionId: null, ct);
        return OkSanitized(result);
    }

    /// <summary>
    /// قائمة «تذكيرات الشطب» بإسقاط قراءة (P3-04c): نفس الصلاحية/النطاق/الترتيب
    /// والترقيم (الأقدم ركودًا أولًا) لكن بلا `WithStandardIncludes`.
    /// </summary>
    [HttpGet("strike-off-reminders/list")]
    public async Task<ActionResult<PagedResult<DocumentListItemDto>>> GetStrikeOffRemindersList(
        [FromQuery] int page = 1, [FromQuery] int perPage = 20,
        [FromQuery] string? cursor = null, CancellationToken ct = default)
    {
        if (!_authorization.Has(User, UserCapability.CanViewStrikeOffReminders))
            return Forbid();

        var result = await _strikeOffReminders.GetListAsync(
            page, perPage, User.GetBranchId(), User.GetUserId(), ownerSectionId: null, ct, cursor: cursor);
        return Ok(result);
    }

    /// <summary>عدد ملفات «تذكيرات الشطب» — لشارة بطاقة لوحة المحامي.</summary>
    [HttpGet("strike-off-reminders/count")]
    public async Task<IActionResult> GetStrikeOffRemindersCount(CancellationToken ct)
    {
        if (!_authorization.Has(User, UserCapability.CanViewStrikeOffReminders))
            return Forbid();

        var count = await _strikeOffReminders.GetCountAsync(
            User.GetBranchId(), User.GetUserId(), ownerSectionId: null, ct);
        return Ok(new { count });
    }

    /// <summary>إلغاء تذكير الشطب لملف يملكه المحامي (يُخفيه من البطاقة حتى إجراء أحدث).</summary>
    [HttpPost("{id:int}/strike-off-reminder/dismiss")]
    public async Task<IActionResult> DismissStrikeOffReminder(int id, CancellationToken ct)
    {
        if (!_authorization.Has(User, UserCapability.CanViewStrikeOffReminders))
            return Forbid();

        return await _dismissStrikeOffReminder.HandleAsync(
                new DismissStrikeOffReminderCommand(id, User.GetUserId(), ActorName), ct)
            ? NoContent()
            : NotFound();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var doc = await _getDocument.HandleAsync(new GetDocumentQuery(id), ct);
        if (doc is null) return NotFound();
        var denied = await RequireDocumentAccessAsync(doc, DocumentAction.ReadOrFollow, ct);
        if (denied is not null) return denied;
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
        var denied = await RequireDocumentAccessAsync(doc, DocumentAction.ReadOrFollow, ct);
        if (denied is not null) return denied;
        return Ok(await _documents.GetBaseNumberHistoryAsync(id, ct));
    }

    /// <summary>سجل أرقام ملف محذوف (مع الملغاة) — من صفحة المحذوفات بنفس صلاحياتها.</summary>
    [HttpGet("deleted/{id:int}/base-numbers")]
    public async Task<IActionResult> GetDeletedBaseNumberHistory(int id, CancellationToken ct)
    {
        var doc = await _documents.GetDeletedAsync(id, ct);
        if (doc is null) return NotFound();
        var denied = await RequireDocumentAccessAsync(doc, DocumentAction.Read, ct);
        if (denied is not null) return denied;
        return Ok(await _documents.GetDeletedBaseNumberHistoryAsync(id, ct));
    }

    /// <summary>تصحيح رقم أساس سنة محددة موضعيًا (لا حذف ولا صف جديد).</summary>
    [HttpPut("{id:int}/base-numbers/{year:int}")]
    public async Task<IActionResult> CorrectBaseNumber(
        int id, int year, [FromBody] CorrectBaseNumberRequest request, CancellationToken ct)
    {
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.Rotate, ct);
        if (denied is not null) return denied;
        try
        {
            await _documents.CorrectBaseNumberAsync(id, year, request, User.GetUserId(), ActorName, ct);
            return NoContent();
        }
        catch (DocumentConflictException e)
        {
            return ApiProblem.Result(HttpContext, StatusCodes.Status409Conflict, e.Code, ApiErrorMessages.From(e));
        }
    }

    /// <summary>التراجع عن تدوير السنة الحالية (الصف يبقى معروضًا «ملغى»).</summary>
    [HttpDelete("{id:int}/base-numbers/{year:int}")]
    public async Task<IActionResult> UndoRotation(int id, int year, CancellationToken ct)
    {
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.Rotate, ct);
        if (denied is not null) return denied;
        try
        {
            await _documents.UndoRotationAsync(id, year, User.GetUserId(), ActorName, ct);
            return NoContent();
        }
        catch (DocumentConflictException e)
        {
            return ApiProblem.Result(HttpContext, StatusCodes.Status409Conflict, e.Code, ApiErrorMessages.From(e));
        }
    }

    /// <summary>
    /// سجل تعديلات الملف على مستوى الحقول (قبل/بعد) — لصاحب الملف ورئيس قسمه
    /// والمدير/المشرف. أداة المراجعة المؤسسية؛ لا تُفتح لمتابعي الإنابة/الاستئناف.
    /// </summary>
    [HttpGet("{id:int}/changes")]
    public async Task<IActionResult> GetChanges(
        int id, [FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.Read, ct);
        if (denied is not null) return denied;
        return Ok(await _auditLogs.GetDocumentChangesAsync(id, page, perPage, ct));
    }

    [HttpPost]
    [SkipIdempotency]
    public async Task<IActionResult> Create([FromBody] DocumentUpsertRequest request, CancellationToken ct)
    {
        // إدخال الملفات محصور بالمحامي: لا إدخال للرؤساء، وقراءة مطلقة للمدير/المشرف.
        if (!_authorization.Has(User, UserCapability.CanEditDocuments))
            return Forbid();

        try
        {
            // RF-011: مفتاح عدم التكرار ترويسة اختيارية — التكرار يُردّ `200` بالجسم المخزن نفسه.
            var doc = await _createDocument.HandleAsync(new CreateDocumentCommand(
                request, User.GetUserId(), ActorName, User.GetBranchId(), IdempotencyKeyHeader()), ct);
            return CreatedAtAction(nameof(Get), new { id = doc.Id }, Sanitize(doc));
        }
        catch (IdempotentReplayException r)
        {
            return Content(r.ResponseBody, "application/json");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] DocumentUpsertRequest request, CancellationToken ct)
    {
        // التعديل محصور بالمحامي (للملفات التي يملكها).
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.Edit, ct);
        if (denied is not null) return denied;

        var updated = await _updateDocument.HandleAsync(
            new UpdateDocumentCommand(id, request, ActorName, User.GetUserId()), ct);
        return updated is null ? NotFound() : Ok(Sanitize(updated));
    }

    /// <summary>
    /// إضافة متدخل (شخص طبيعي/اعتباري/جهة عامة) إلى الملف — المحامي المالك فقط.
    /// تُحفظ فورًا وتُزامن وقعة «طلب تدخل» في المعاملة نفسها (لا تنتظر حفظ النموذج).
    /// </summary>
    [HttpPost("{id:int}/interventions")]
    public async Task<IActionResult> AddIntervention(
        int id, [FromBody] DocumentInterventionUpsertRequest request, CancellationToken ct)
    {
        // F-01: السياسة تحلّ المورد بإسقاط خفيف — التحميل الكامل الوحيد في هذا المسار
        // هو القراءة الطازجة تحت القفل داخل خدمة المتدخلين.
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.Edit, ct);
        if (denied is not null) return denied;

        var created = await _addIntervention.HandleAsync(new AddDocumentInterventionCommand(
            id, request, User.GetUserId(), ActorName), ct);
        return Ok(created);
    }

    /// <summary>تعديل متدخل قائم ومزامنة وقعته — المحامي المالك فقط.</summary>
    [HttpPut("{id:int}/interventions/{interventionId:int}")]
    public async Task<IActionResult> UpdateIntervention(
        int id, int interventionId, [FromBody] DocumentInterventionUpsertRequest request, CancellationToken ct)
    {
        // F-01: نفس الإسقاط الخفيف عبر السياسة — الخدمة تحمّل المستند مرة واحدة للتعديل.
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.Edit, ct);
        if (denied is not null) return denied;

        var updated = await _updateIntervention.HandleAsync(
            new UpdateDocumentInterventionCommand(
                id, interventionId, request, User.GetUserId(), ActorName), ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>حذف متدخل وورثته ووقعته — المحامي المالك فقط (بتأكيد في الواجهة).</summary>
    [HttpDelete("{id:int}/interventions/{interventionId:int}")]
    public async Task<IActionResult> DeleteIntervention(
        int id, int interventionId, CancellationToken ct)
    {
        // F-01: نفس الإسقاط الخفيف عبر السياسة — الخدمة تحمّل المستند مرة واحدة للحذف.
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.Edit, ct);
        if (denied is not null) return denied;

        return await _deleteIntervention.HandleAsync(
            new DeleteDocumentInterventionCommand(id, interventionId, ActorName), ct)
            ? NoContent()
            : NotFound();
    }

    [HttpGet("rotate")]
    public async Task<IActionResult> GetRotationList([FromQuery] int page = 1, [FromQuery] int perPage = 20, CancellationToken ct = default)
    {
        // تدوير أرقام الأساس للمحامي فقط — على ملفاته.
        if (!_authorization.Has(User, UserCapability.CanRotate))
            return Forbid();

        return Ok(await _documents.GetRotationListAsync(User.GetUserId(), page, perPage, ct));
    }

    [HttpPut("rotate")]
    public async Task<IActionResult> SaveBaseNumbers([FromBody] SaveBaseNumbersRequest request, CancellationToken ct)
    {
        // تدوير أرقام الأساس للمحامي فقط — على ملفاته.
        if (!_authorization.Has(User, UserCapability.CanRotate))
            return Forbid();

        await _documents.SaveBaseNumbersAsync(
            User.GetUserId(), request.Entries, ActorName, request.WarningToken, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        // الحذف المنطقي محصور بالمحامي صاحب الملف.
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.Delete, ct);
        if (denied is not null) return denied;

        return await _documents.DeleteAsync(id, ActorName, ct) ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id, CancellationToken ct)
    {
        // استعادة المحذوف منطقياً من اختصاص المحامي صاحب الملف فقط.
        var doc = await _documents.GetDeletedAsync(id, ct);
        if (doc is null) return NotFound();
        var denied = await RequireDocumentAccessAsync(doc, DocumentAction.Restore, ct);
        if (denied is not null) return denied;

        return await _restoreDocument.HandleAsync(
            new RestoreDocumentCommand(id, ActorName), ct)
            ? Ok(new { message = "تمت استعادة المستند" })
            : NotFound();
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] StatusRequest request, CancellationToken ct)
    {
        // تغيير حالة المستند محصور بالمحامي (للملفات التي يملكها).
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ChangeStatus, ct);
        if (denied is not null) return denied;

        var ok = await _status.UpdateStatusAsync(id, request.Status, request.Fields ?? new(), ActorName, ct, request.Version);
        return ok ? Ok(new { message = "تم تحديث الحالة" }) : DocumentNotFound();
    }

    [HttpPost("{id:int}/revert-status")]
    public async Task<IActionResult> RevertStatus(int id, [FromBody] StatusRequest request, CancellationToken ct)
    {
        // التراجع عن الحالة (من تريث/منفذ بالتسوية/منفذ جبريا إلى متداول) محصور بالمحامي
        // (للملفات التي يملكها) — بموجب كتاب الجهة العامة بالسير بالملف.
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ChangeStatus, ct);
        if (denied is not null) return denied;

        var ok = await _status.RevertStatusAsync(id, request.Fields ?? new(), ActorName, ct, request.Version);
        return ok ? Ok(new { message = "عُد الملف إلى المتداول" }) : DocumentNotFound();
    }

    [HttpPost("{id:int}/return-referred-to-start")]
    public async Task<IActionResult> ReturnFromReferredToStart(int id, [FromBody] ReturnReferredToStartRequest request, CancellationToken ct)
    {
        // العودة من «محال الى البداية» (بنتيجتين: استعادة «منفذ جزئيًا» حسب اللازمة وإلا
        // «متداول») محصورة بالمحامي (للملفات التي يملكها) — بصلاحية «تغيير الحالة» كنظيريها
        // التراجع/الاعتبار، وبلا حقول إلزامية (رقم الملف الجديد الاختياري يفعّل التجديد).
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ChangeStatus, ct);
        if (denied is not null) return denied;

        var ok = await _status.ReturnFromReferredToStartAsync(id, request ?? new(), ActorName, ct);
        return ok ? Ok(new { message = "أعيد السير بالملف" }) : DocumentNotFound();
    }

    [HttpPost("{id:int}/consider-executed-by-delegation")]
    public async Task<IActionResult> ConsiderExecutedByDelegation(int id, [FromBody] StatusRequest request, CancellationToken ct)
    {
        // «اعتبار الملف منفذًا كاملًا بهذا البيع» (إغلاق «منفذ جبريا — منفذ جزئيا» الذي
        // فُعّل تلقائيًا بإتمام إنابة) محصور بالمحامي (للملفات التي يملكها) — يُلزم إدخال
        // «تاريخ تحويل بدل المبيع للجهة العامة» مع «رقم الإشعار» الاختياري.
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ChangeStatus, ct);
        if (denied is not null) return denied;

        var ok = await _status.ConsiderExecutedByDelegationAsync(id, request.Fields ?? new(), ActorName, ct, request.Version);
        return ok ? Ok(new { message = "اعتُبر الملف منفذًا كاملًا بهذا البيع" }) : DocumentNotFound();
    }

    [HttpPost("{id:int}/executed-status")]
    public async Task<IActionResult> SetExecutedStatus(int id, [FromBody] ExecutedStatusRequest request, CancellationToken ct)
    {
        // تغيير حالة وضع «الجهة العامة منفذ عليها» محصور بالمحامي (للملفات التي يملكها) —
        // يعمل على ملفات صفة executed فقط.
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ChangeStatus, ct);
        if (denied is not null) return denied;

        var ok = await _status.UpdateExecutedStatusAsync(id, request.Status, request, ActorName, ct);
        return ok ? Ok(new { message = "تم تحديث حالة وضع «الجهة العامة منفذ عليها»" }) : DocumentNotFound();
    }

    [HttpPost("{id:int}/restore-struck-off")]
    public async Task<IActionResult> RestoreStruckOff(int id, [FromBody] RenewalRequest request, CancellationToken ct)
    {
        // إعادة ملف مشطوب إلى المتداول من اختصاص المحامي صاحب الملف فقط
        // (بذات حكم الاستعادة في المحذوفات).
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.Restore, ct);
        if (denied is not null) return denied;

        return await _status.RestoreStruckOffAsync(id, request, ActorName, ct)
            ? Ok(new { message = "أعيد الملف المشطوب إلى المتداول" })
            : NotFound();
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
        // مفتاح تعطيل مؤقت: يمنع توليد Word في كل البيئات حتى تفعيله صراحةً بعد
        // توحيد النماذج المولدة. يُرجع 503 ورسالة مفهومة تعرضها الواجهة.
        if (!_generationOptions.Value.Enabled)
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status503ServiceUnavailable,
                ApiErrorCodes.GenerationDisabled,
                _generationOptions.Value.DisabledMessage);

        if (string.IsNullOrWhiteSpace(template))
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.ValidationFailed,
                "يرجى تحديد نوع المستند المطلوب توليده",
                field: "template");

        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        var denied = await RequireDocumentAccessAsync(doc, DocumentAction.Read, ct);
        if (denied is not null) return denied;

        // توليد المستندات محصور بنظام «طالبة تنفيذ»: ملفات عائلة وضع «منفذ عليه» لا تُولَّد.
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status400BadRequest,
                ApiErrorCodes.GenerationNotAllowed,
                "لا يُولَّد مستند لملفات وضع «الجهة العامة منفذ عليها» و«عرض وايداع»");

        try
        {
            var result = await _generator.GenerateAsync(id, template, recipient, estateIds, heirId, ct, ActorName);
            return File(result.Bytes, WordContentType, result.FileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (FileNotFoundException)
        {
            // لا يُكشف مسار قالب الخادم للمستخدم؛ تُرجع رسالة عامة.
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status500InternalServerError,
                ApiErrorCodes.ServerError,
                "القالب غير متوفر على الخادم");
        }
    }

    [HttpGet("owner/{lawyerId:int}/count")]
    public async Task<IActionResult> CountFilesByOwner(int lawyerId, CancellationToken ct)
    {
        // عدد ملفات المحامي (معاينة قبل النقل الجماعي) — رئيس القسم والشعبة
        // (ضمن فرعه) فقط — بالنطاق نفسه (§5.5): المعاينة تطابق المنقول فعلًا.
        if (!_authorization.Has(User, UserCapability.CanTransferDocuments))
            return Forbid();

        // رئيس القسم والشعبة بلا فرع لا يملك نطاقًا صالحًا للنقل (يُمنع صراحةً كبقية عمليات الفرع).
        var scopeBranchId = User.GetBranchId();
        if (scopeBranchId is null)
            return Forbid();
        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        var count = await _documents.CountFilesByOwnerAsync(lawyerId, scopeBranchId, ct, scope.OwnerSectionId);
        return Ok(new { count });
    }

    [HttpPost("transfer-all")]
    [SkipIdempotency]
    public async Task<IActionResult> TransferAll([FromBody] TransferAllRequest request, CancellationToken ct)
    {
        // نقل كامل ملفات محامٍ إلى محامٍ آخر بجميع الحالات — رئيس القسم والشعبة
        // (ضمن فرعه) فقط — بتقاطع النطاق (§5.5): ملفات المصدر ضمن نطاق المنفِّذ فقط.
        if (!_authorization.Has(User, UserCapability.CanTransferDocuments))
            return Forbid();

        // رئيس القسم والشعبة بلا فرع لا يملك نطاقًا صالحًا للنقل (يُمنع صراحةً كبقية عمليات الفرع).
        var scopeBranchId = User.GetBranchId();
        if (scopeBranchId is null)
            return Forbid();
        var scope = _authorization.ResolveListScope(User);
        if (scope is null) return Forbid();

        try
        {
            // RF-011: التكرار بنفس المفتاح يُعيد العدد المخزن نفسه.
            var transferredCount = await _documents.TransferAllAsync(
                request.SourceLawyerId, request.TargetLawyerId, scopeBranchId, ActorName, ct,
                IdempotencyKeyHeader(), scope.OwnerSectionId, User.GetUserId());
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
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status409Conflict,
                            ApiErrorCodes.Conflict,
                            ApiErrorMessages.From(e));
        }
    }

    [HttpPost("{id:int}/transfer")]
    public async Task<IActionResult> Transfer(int id, [FromBody] TransferDocumentRequest request, CancellationToken ct)
    {
        // نقل الملفات بين المحامين — رئيس القسم والشعبة (ضمن فرعه) فقط —
        // والملف نفسه ضمن نطاق المنفِّذ.
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.Transfer, ct);
        if (denied is not null) return denied;

        try
        {
            var updated = await _documents.TransferAsync(
                id, request.TargetLawyerId, ActorName, ct, User.GetUserId());
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (DocumentConflictException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status409Conflict,
                            ApiErrorCodes.Conflict,
                            ApiErrorMessages.From(e));
        }
    }

    [HttpGet("{id:int}/actions")]
    public async Task<IActionResult> GetActions(int id, CancellationToken ct)
    {
        var doc = await _documents.GetAsync(id, ct);
        if (doc is null) return NotFound();
        var denied = await RequireDocumentAccessAsync(doc, DocumentAction.ReadOrFollow, ct);
        if (denied is not null) return denied;

        var actions = await _executionActions.GetExecutionActionsAsync(id, ct);
        return Ok(actions);
    }

    [HttpPost("{id:int}/actions")]
    public async Task<IActionResult> AddAction(int id, [FromBody] AddExecutionActionRequest request, CancellationToken ct)
    {
        // الإضافة للمحامي فقط
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ManageActions, ct);
        if (denied is not null) return denied;

        var action = await _addExecutionAction.HandleAsync(new AddExecutionActionCommand(
            id, request, User.GetUserId(), ActorName), ct);
        return Ok(action);
    }

    [HttpPut("{id:int}/actions/{actionId:int}")]
    public async Task<IActionResult> UpdateAction(int id, int actionId, [FromBody] UpdateExecutionActionRequest request, CancellationToken ct)
    {
        // التعديل للمحامي فقط
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ManageActions, ct);
        if (denied is not null) return denied;

        var action = await _updateExecutionAction.HandleAsync(new UpdateExecutionActionCommand(
            id, actionId, request, ActorName), ct);
        if (action is null) return NotFound();
        return Ok(action);
    }

    /// <summary>
    /// نقل تذكير إجراء إلى يوم استحقاق جديد من نافذة اليوم — المحامي المالك (المدخل `dueDate`
    /// نفسه). يكتب `ReminderBaseDate` وحده ولا يغيّر تاريخ الإجراء.
    /// </summary>
    [HttpPost("{id:int}/actions/{actionId:int}/reschedule")]
    public async Task<IActionResult> RescheduleReminder(int id, int actionId, [FromBody] RescheduleActionReminderRequest? request, CancellationToken ct)
    {
        // النقل للمحامي فقط
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ManageActions, ct);
        if (denied is not null) return denied;

        var action = await _rescheduleActionReminder.HandleAsync(
            new RescheduleActionReminderCommand(
                id, actionId, request?.NewDueDate, ActorName, request?.Version), ct);
        return Ok(action);
    }

    [HttpDelete("{id:int}/actions/{actionId:int}")]
    public async Task<IActionResult> DeleteAction(int id, int actionId, CancellationToken ct)
    {
        // الحذف للمحامي فقط
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ManageActions, ct);
        if (denied is not null) return denied;

        var deleted = await _deleteExecutionAction.HandleAsync(
            new DeleteExecutionActionCommand(id, actionId, ActorName), ct);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// إنشاء/تعديل تذكير إجراء قائم (مدة/لون) — مسار مستقل عن تعديل الإجراء: تعديل النص أو
    /// التاريخ لا يكتب حالة تذكير، والعكس كذلك. إرسال الحقلين فارغين = إلغاء (كأثر `DELETE`).
    /// </summary>
    [HttpPut("{id:int}/actions/{actionId:int}/reminder")]
    public async Task<IActionResult> SetReminder(
        int id, int actionId, [FromBody] SetActionReminderRequest request, CancellationToken ct)
    {
        // إدارة التذكير للمحامي فقط
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ManageActions, ct);
        if (denied is not null) return denied;

        var action = await _setActionReminder.HandleAsync(
            new SetActionReminderCommand(id, actionId, request, ActorName), ct);
        return Ok(action);
    }

    [HttpDelete("{id:int}/actions/{actionId:int}/reminder")]
    public async Task<IActionResult> ClearReminder(int id, int actionId, CancellationToken ct)
    {
        // إلغاء التذكير للمحامي فقط
        var denied = await RequireDocumentAccessAsync(id, DocumentAction.ManageActions, ct);
        if (denied is not null) return denied;

        return await _clearReminder.HandleAsync(
            new ClearActionReminderCommand(id, actionId, ActorName), ct)
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

    /// <summary>`404` موحّد بالجسم العربي لعمليات الحالة (بدل جسم فارغ).</summary>
    private ObjectResult DocumentNotFound() =>
        ApiProblem.Result(
            HttpContext,
            StatusCodes.Status404NotFound,
            ApiErrorCodes.NotFound,
            DocumentNotFoundMessage);

}
