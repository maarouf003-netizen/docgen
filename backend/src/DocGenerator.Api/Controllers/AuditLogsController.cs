using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "manager,admin,head")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _logs;

    public AuditLogsController(IAuditLogService logs) => _logs = logs;

    private UserRole Role => User.GetRoleEnum();

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> Search(
        [FromQuery] string? userName,
        [FromQuery] string? actionType,
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 20,
        CancellationToken ct = default)
    {
        // RF-007: رئيس القسم مقصور على تدقيق فرعه (مستندات فرعه أو فاعلو فرعه) —
        // المدير/المشرف بلا نطاق كما قبل. نفس نمط scopeBranchId في UserManagementController.
        var scopeBranchId = Role == UserRole.Head ? User.GetBranchId() : null;
        return Ok(await _logs.SearchAsync(userName, actionType, page, perPage, ct, scopeBranchId));
    }
}
