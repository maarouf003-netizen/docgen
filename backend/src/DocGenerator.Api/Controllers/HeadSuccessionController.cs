using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/head-succession")]
[Authorize(Roles = "manager,admin")]
public class HeadSuccessionController : ControllerBase
{
    private readonly IQueryHandler<ListSuccessionQuery, List<HeadSuccessionDto>> _listSuccession;

    public HeadSuccessionController(
        IQueryHandler<ListSuccessionQuery, List<HeadSuccessionDto>> listSuccession)
    {
        _listSuccession = listSuccession;
    }

    /// <summary>سجل تعاقب الرئاسة — المدير والمشرف فقط (قرار §2.17)؛ الأحدث أولًا.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? branchId, CancellationToken ct)
    {
        return Ok(await _listSuccession.HandleAsync(new ListSuccessionQuery(branchId), ct));
    }
}
