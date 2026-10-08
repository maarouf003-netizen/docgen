using DocGenerator.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/head-succession")]
[Authorize(Roles = "manager,admin")]
public class HeadSuccessionController : ControllerBase
{
    private readonly IUserManagementService _users;

    public HeadSuccessionController(IUserManagementService users)
    {
        _users = users;
    }

    /// <summary>سجل تعاقب الرئاسة — المدير والمشرف فقط (قرار §2.17)؛ الأحدث أولًا.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? branchId, CancellationToken ct)
    {
        return Ok(await _users.ListSuccessionAsync(branchId, ct));
    }
}
