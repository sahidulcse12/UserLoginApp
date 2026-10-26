using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstractions;

namespace AuthSubsystem.APIs.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IServiceBus _serviceBus;

    public RolesController(IServiceBus serviceBus)
    {
        _serviceBus = serviceBus;
    }

    // Get all Keycloak users with their roles and permissions.
    // Can dynamically filter by userType (e.g. ?userType=mis, ?userType=corporate, ?userType=mybdjobs).
    // Defaults to "mis" if not specified, or omit to get all.
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] string? userType, CancellationToken cancellationToken)
    {
        var result = await _serviceBus.SendCommandAsync(new GetAllUsersWithRolesQuery(userType), cancellationToken);
        return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    // Specifically get all MIS-Users with their roles and permissions.
    [HttpGet("mis-users")]
    public async Task<IActionResult> GetMisUsers(CancellationToken cancellationToken)
    {
        var result = await _serviceBus.SendCommandAsync(new GetAllUsersWithRolesQuery("mis"), cancellationToken);
        return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    // Get permissions and assigned roles for a specific user.
    [HttpGet("user-permissions/{username}")]
    public async Task<IActionResult> GetUserPermissions(string username, CancellationToken cancellationToken)
    {
        var result = await _serviceBus.SendCommandAsync(new GetUserPermissionsQuery(username), cancellationToken);
        return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    // Assign a role and permissions/attributes to any user dynamically.
    // Can assign to MIS-Users, Corporate-Users, or MyBdjobs-Users.
    // Restricted to users who have the 'admin' role in Keycloak.
    [HttpPost("assign-role")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _serviceBus.SendCommandAsync(new AssignRoleCommand(request), cancellationToken);
        return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }
}
