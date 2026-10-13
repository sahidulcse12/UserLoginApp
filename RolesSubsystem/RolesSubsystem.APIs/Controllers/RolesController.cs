using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RolesSubsystem.DTOs;
using RolesSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstractions;

namespace RolesSubsystem.APIs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IServiceBus _serviceBus;

    public RolesController(IServiceBus serviceBus)
    {
        _serviceBus = serviceBus;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _serviceBus.SendCommandAsync(new GetRolesCommand());
        return Ok(roles);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var role = await _serviceBus.SendCommandAsync(new GetRoleByIdCommand(id));
        return role == null ? NotFound() : Ok(role);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        var role = await _serviceBus.SendCommandAsync(new CreateRoleCommand(request));
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
    }
}
