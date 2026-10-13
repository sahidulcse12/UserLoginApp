using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Orchestrators;

namespace UserLoginApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IRoleOrchestrator _roleOrchestrator;

    public RolesController(IRoleOrchestrator roleOrchestrator)
    {
        _roleOrchestrator = roleOrchestrator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _roleOrchestrator.GetAllRolesAsync();
        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var role = await _roleOrchestrator.GetRoleByIdAsync(id);
        return role == null ? NotFound() : Ok(role);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        var role = await _roleOrchestrator.CreateRoleAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
    }
}
