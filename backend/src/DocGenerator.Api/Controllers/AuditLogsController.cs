using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "manager,admin,head,subhead")]
public class AuditLogsController : ControllerBase
{
    private readonly IQueryHandler<SearchAuditLogsQuery, PagedResult<AuditLogDto>> _logs;
    private readonly IUserCapabilities _capabilities;

    public AuditLogsController(
        IQueryHandler<SearchAuditLogsQuery, PagedResult<AuditLogDto>> logs,
        IUserCapabilities capabilities)
    {
        _logs = logs;
        _capabilities = capabilities;
    }

    private bool IsHead => _capabilities.Has(User, UserCapability.IsHead);
    private bool IsSubHead => _capabilities.Has(User, UserCapability.IsSubHead);

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> Search(
        [FromQuery] string? userName,
        [FromQuery] string? actionType,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        // RF-007: رئيس القسم مقصور على تدقيق فرعه (مستندات فرعه أو فاعلو فرعه) —
        // F6: ورئيس الشعبة على تدقيق شعبته الدائري (قرار 23) — مستندات دوائر شعبته
        // أو فاعلو شعبته. شعبة برمز بلا شعبة مرفوضة (قرار §2.16) بدل التدهور لنطاق الفرع.
        // المدير/المشرف بلا نطاق كما قبل. نفس نمط scopeBranchId في UserManagementController.
        // L-05 (2026-10-17): رئيس قسم بلا رمز فرع مرفوض صريحًا بدل النطاق الإجمالي —
        // تحوّط دفاعي موازٍ لمانع القاعدة (CK_Users_BranchRequiredForBranchRoles) وحارس الدخول.
        var scopeBranchId = IsHead ? User.GetBranchId() : null;
        if (IsHead && scopeBranchId is null) return Forbid();
        int? scopeSectionId = null;
        if (IsSubHead)
        {
            scopeSectionId = User.GetSectionId();
            if (scopeSectionId is null) return Forbid();
        }
        return Ok(await _logs.HandleAsync(
            new SearchAuditLogsQuery(userName, actionType, page, perPage, scopeBranchId, scopeSectionId), ct));
    }
}
