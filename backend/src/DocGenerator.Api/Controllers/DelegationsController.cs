using DocGenerator.Api.Authorization;
using DocGenerator.Api.Http;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Delegations;
using DocGenerator.Application.UseCases.Documents;
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
    private readonly IQueryHandler<GetDocumentQuery, DocumentResponse?> _getDocument;
    private readonly IUserCapabilities _capabilities;
    private readonly IQueryHandler<GetDocumentDelegationsQuery, List<DelegationDto>> _documentDelegations;
    private readonly IQueryHandler<GetPendingDelegationsQuery, List<DelegationDto>> _pendingDelegations;
    private readonly IQueryHandler<CountPendingDelegationsQuery, int> _pendingDelegationsCount;
    private readonly ICommandHandler<DeleteDocumentDelegationCommand, bool> _deleteDelegation;
    private readonly ICommandHandler<CreateDocumentDelegationCommand, DelegationDto> _createDelegation;
    private readonly ICommandHandler<UpdateDocumentDelegationCommand, DelegationDto?> _updateDelegation;
    private readonly ICommandHandler<RedirectDocumentDelegationCommand, DelegationDto?> _redirectDelegation;
    private readonly ICommandHandler<RecallDelegationRedirectCommand, DelegationDto?> _recallRedirect;
    private readonly ICommandHandler<RejectDocumentDelegationCommand, DelegationDto?> _rejectDelegation;
    private readonly ICommandHandler<AssignDocumentDelegationCommand, DelegationDto?> _assignDelegation;
    private readonly ICommandHandler<RegisterDocumentDelegationCommand, DelegationDto?> _registerDelegation;
    private readonly ICommandHandler<CompleteDocumentDelegationCommand, DelegationDto?> _completeDelegation;

    public DelegationsController(
        IQueryHandler<GetDocumentQuery, DocumentResponse?> getDocument,
        IUserCapabilities capabilities,
        IQueryHandler<GetDocumentDelegationsQuery, List<DelegationDto>> documentDelegations,
        IQueryHandler<GetPendingDelegationsQuery, List<DelegationDto>> pendingDelegations,
        IQueryHandler<CountPendingDelegationsQuery, int> pendingDelegationsCount,
        ICommandHandler<DeleteDocumentDelegationCommand, bool> deleteDelegation,
        ICommandHandler<CreateDocumentDelegationCommand, DelegationDto> createDelegation,
        ICommandHandler<UpdateDocumentDelegationCommand, DelegationDto?> updateDelegation,
        ICommandHandler<RedirectDocumentDelegationCommand, DelegationDto?> redirectDelegation,
        ICommandHandler<RecallDelegationRedirectCommand, DelegationDto?> recallRedirect,
        ICommandHandler<RejectDocumentDelegationCommand, DelegationDto?> rejectDelegation,
        ICommandHandler<AssignDocumentDelegationCommand, DelegationDto?> assignDelegation,
        ICommandHandler<RegisterDocumentDelegationCommand, DelegationDto?> registerDelegation,
        ICommandHandler<CompleteDocumentDelegationCommand, DelegationDto?> completeDelegation)
    {
        _getDocument = getDocument;
        _capabilities = capabilities;
        _documentDelegations = documentDelegations;
        _pendingDelegations = pendingDelegations;
        _pendingDelegationsCount = pendingDelegationsCount;
        _deleteDelegation = deleteDelegation;
        _createDelegation = createDelegation;
        _updateDelegation = updateDelegation;
        _redirectDelegation = redirectDelegation;
        _recallRedirect = recallRedirect;
        _rejectDelegation = rejectDelegation;
        _assignDelegation = assignDelegation;
        _registerDelegation = registerDelegation;
        _completeDelegation = completeDelegation;
    }

    private string? ActorName => User.Identity?.Name;

    private bool HasFullAccess => _capabilities.Has(User, UserCapability.HasFullAccess);
    private bool IsHeadOrSubHead => _capabilities.Has(User, UserCapability.IsHeadOrSubHead);
    private bool IsLawyer => _capabilities.Has(User, UserCapability.IsLawyer);
    private bool IsHead => _capabilities.Has(User, UserCapability.IsHead);
    private bool IsSubHead => _capabilities.Has(User, UserCapability.IsSubHead);
    private bool CanManage => _capabilities.Has(User, UserCapability.CanManageDelegations);
    private bool CanApprove => _capabilities.Has(User, UserCapability.CanApproveDelegations);

    /// <summary>
    /// نطاق الرؤية (§7.1): رئيس بلا فرع مرفوض، ورئيس شعبة بلا شعبة مرفوض —
    /// بلا تدهور لنطاق أوسع. **غياب الفرع هنا رفض صلاحية `403`** لأنه يمنع قراءة
    /// نطاق غير محدد (قرار `D11` الموحّد 2026-11-17: كتابة `400`/قراءة `403`).
    /// </summary>
    private ActionResult? RequireApproverScope(out int branchId, out int? ownerSectionId)
    {
        branchId = 0;
        ownerSectionId = null;
        var branch = User.GetBranchId();
        if (branch is null)
            return ApiProblem.Result(
                HttpContext,
                StatusCodes.Status403Forbidden,
                ApiErrorCodes.BranchRequired,
                "الرئيس دون فرع لا يمكنه الاطلاع على طلبات الإنابة");
        branchId = branch.Value;
        if (IsSubHead)
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
        if (IsLawyer)
            return doc.CreatedById == User.GetUserId();
        if (doc.BranchId != User.GetBranchId())
            return false;
        if (IsHead)
            return doc.ExecutionCircuitId is null || !doc.SectionId.HasValue;
        if (IsSubHead)
            return doc.SectionId.HasValue && doc.SectionId == User.GetSectionId();
        return false;
    }

    /// <summary>إنابات ملف (بطاقة «تشعبات الملف»): منيبة (المصدر) أو مناب (إنابته) — تشمل بطاقة المناب (§5.7).</summary>
    [HttpGet("documents/{documentId:int}/delegations")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> ListForDocument(int documentId, CancellationToken ct)
    {
        var doc = await _getDocument.HandleAsync(new GetDocumentQuery(documentId), ct);
        if (doc is null)
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status404NotFound,
                            ApiErrorCodes.NotFound,
                            "الملف غير موجود");
        if (!CanAccess(doc))
            return Forbid();
        try
        {
            return Ok(await _documentDelegations.HandleAsync(new GetDocumentDelegationsQuery(documentId), ct));
        }
        catch (ArgumentException e)
        {
            return ApiProblem.Result(
                            HttpContext,
                            StatusCodes.Status404NotFound,
                            ApiErrorCodes.NotFound,
                            ApiErrorMessages.From(e));
        }
    }

    /// <summary>تسطير إنابة جديدة على ملف منيب — محامي الملف المالك فقط.</summary>
    [HttpPost("documents/{documentId:int}/delegations")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Create(int documentId, [FromBody] UpsertDelegationRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        var created = await _createDelegation.HandleAsync(
            new CreateDocumentDelegationCommand(documentId, request, User.GetUserId(), ActorName), ct);
        return CreatedAtAction(nameof(ListForDocument), new { documentId }, created);
    }

    /// <summary>تعديل إنابة معلّقة — محامي الملف المنيب المالك فقط.</summary>
    [HttpPut("delegations/{id:int}")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertDelegationRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        var updated = await _updateDelegation.HandleAsync(
            new UpdateDocumentDelegationCommand(id, request, User.GetUserId(), ActorName), ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>حذف إنابة معلّقة — محامي الملف المنيب المالك فقط.</summary>
    [HttpDelete("delegations/{id:int}")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        var ok = await _deleteDelegation.HandleAsync(
            new DeleteDocumentDelegationCommand(id, User.GetUserId(), ActorName), ct);
        return ok ? NoContent() : NotFound();
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
        return Ok(await _pendingDelegations.HandleAsync(
            new GetPendingDelegationsQuery(branchId, ownerSectionId, rejectedOnly), ct));
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
            count = await _pendingDelegationsCount.HandleAsync(
                new CountPendingDelegationsQuery(branchId, ownerSectionId, false), ct),
            rejectedCount = await _pendingDelegationsCount.HandleAsync(
                new CountPendingDelegationsQuery(branchId, ownerSectionId, true), ct),
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
            var assigned = await _assignDelegation.HandleAsync(
                new AssignDocumentDelegationCommand(
                    id, request, User.GetUserId(), User.GetBranchId(), ActorName), ct);
            return assigned is null ? NotFound() : Ok(assigned);
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

    /// <summary>توجيه إنابة خارجية معلّقة لشعبة في الفرع المناب — رئيس قسم الفرع المناب فقط (§7.3).</summary>
    [HttpPost("delegations/{id:int}/redirect")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Redirect(int id, [FromBody] RedirectDelegationRequest request, CancellationToken ct)
    {
        if (!CanApprove)
            return Forbid();
        try
        {
            var redirected = await _redirectDelegation.HandleAsync(
                new RedirectDocumentDelegationCommand(id, request, User.GetUserId(), ActorName), ct);
            return redirected is null ? NotFound() : Ok(redirected);
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

    /// <summary>التراجع عن توجيه الشعبة قبل الإسناد — رئيس قسم فرع الاعتماد فقط (§7.3).</summary>
    [HttpPost("delegations/{id:int}/recall-redirect")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> RecallRedirect(int id, CancellationToken ct)
    {
        if (!CanApprove)
            return Forbid();
        try
        {
            var recalled = await _recallRedirect.HandleAsync(
                new RecallDelegationRedirectCommand(id, User.GetUserId(), ActorName), ct);
            return recalled is null ? NotFound() : Ok(recalled);
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

    /// <summary>رفض الدائرة الخطأ برسالة تُعيد المحامي للتصحيح (§7.4) — المعتمد الحالي فقط.</summary>
    [HttpPost("delegations/{id:int}/reject")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectDelegationRequest request, CancellationToken ct)
    {
        if (!CanApprove)
            return Forbid();
        try
        {
            var rejected = await _rejectDelegation.HandleAsync(
                new RejectDocumentDelegationCommand(
                    id, request ?? new RejectDelegationRequest(null), User.GetUserId(), ActorName), ct);
            return rejected is null ? NotFound() : Ok(rejected);
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

    /// <summary>تسجيل الإنابة أصولًا (رقم أساس وتاريخ قيد الملف المناب) — محامي الملف المناب فقط.</summary>
    [HttpPost("delegations/{id:int}/register")]
    [Authorize(Roles = "lawyer,head,subhead,manager,admin")]
    public async Task<IActionResult> Register(int id, [FromBody] RegisterDelegationRequest request, CancellationToken ct)
    {
        if (!CanManage)
            return Forbid();
        var registered = await _registerDelegation.HandleAsync(
            new RegisterDocumentDelegationCommand(id, request, User.GetUserId(), ActorName), ct);
        return registered is null ? NotFound() : Ok(registered);
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
        var completed = await _completeDelegation.HandleAsync(
            new CompleteDocumentDelegationCommand(id, request, User.GetUserId(), ActorName), ct);
        return completed is null ? NotFound() : Ok(completed);
    }
}
