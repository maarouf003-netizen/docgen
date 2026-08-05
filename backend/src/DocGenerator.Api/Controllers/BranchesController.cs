using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/branches")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IRepository<Branch> _branches;

    public BranchesController(IRepository<Branch> branches) => _branches = branches;

    [HttpGet]
    public async Task<ActionResult<List<BranchDto>>> List(CancellationToken ct)
    {
        var branches = await _branches.ListAsync(ct);
        return Ok(branches.Select(b => new BranchDto(b.Id, b.Name, b.Code, b.Address)).ToList());
    }
}
