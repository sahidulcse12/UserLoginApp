using AuthSubsystem.DTOs;
using Microsoft.AspNetCore.Mvc;
using AuthSubsystem.DTOs.Commands;
using Microsoft.AspNetCore.Authorization;
using SharedSubsystem.Handlers.Abstractions;

namespace AuthSubsystem.APIs.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController(IServiceBus serviceBus) : ControllerBase
{
    // Get all Keycloak users with their roles and permissions.
    // Can dynamically filter by userType (e.g. ?userType=mis, ?userType=corporate, ?userType=mybdjobs).
    // Defaults to "mis" if not specified, or omit to get all.
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] string? userType)
    {
        var result = await serviceBus.SendQueryAsync<GetAllUsersWithRolesQuery, AuthResult<IEnumerable<KeycloakUserRoleDto>>>(new GetAllUsersWithRolesQuery(userType));
        return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    // Specifically get all MIS-Users with their roles and permissions.
    [HttpGet("mis-users")]
    public async Task<IActionResult> GetMisUsers()
    {
        var result = await serviceBus.SendQueryAsync<GetAllUsersWithRolesQuery, AuthResult<IEnumerable<KeycloakUserRoleDto>>>(new GetAllUsersWithRolesQuery("mis"));
        return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    // Get permissions and assigned roles for a specific user.
    [HttpGet("user-permissions/{username}")]
    public async Task<IActionResult> GetUserPermissions(string username)
    {
        var result = await serviceBus.SendQueryAsync<GetUserPermissionsQuery, AuthResult<UserPermissionsDto>>(new GetUserPermissionsQuery(username));
        return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    // Assign a role and permissions/attributes to any user dynamically.
    // Can assign to MIS-Users, Corporate-Users, or MyBdjobs-Users.
    // Restricted to users who have the 'admin' role in Keycloak.
    [HttpPost("assign-role")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        var result = await serviceBus.SendCommandAsync(new AssignRoleCommand(request));
        return result.Success ? Ok(result.Data) : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }
}
