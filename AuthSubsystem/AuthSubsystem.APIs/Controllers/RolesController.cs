using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceBus.Handlers;
using SharedSubsystem.Handlers.Events;
using HandlerEventId = SharedSubsystem.Handlers.Events.EventId;

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
        var result = await serviceBus.SendQueryAsync<GetAllUsersWithRolesQuery, IEnumerable<KeycloakUserRoleDto>>(new GetAllUsersWithRolesQuery(userType));

        var events = result is null
            ? EventFactory.CreateFailureEvent(HandlerEventId.ResourceNotFound)
            : EventFactory.CreateSuccessEvent(HandlerEventId.ResourceRetrieved, result);

        return Ok(events);
    }

    // Specifically get all MIS-Users with their roles and permissions.
    [HttpGet("mis-users")]
    public async Task<IActionResult> GetMisUsers()
    {
        var result = await serviceBus.SendQueryAsync<GetAllUsersWithRolesQuery, IEnumerable<KeycloakUserRoleDto>>(new GetAllUsersWithRolesQuery("mis"));

        var events = result is null
            ? EventFactory.CreateFailureEvent(HandlerEventId.ResourceNotFound)
            : EventFactory.CreateSuccessEvent(HandlerEventId.ResourceRetrieved, result);

        return Ok(events);
    }

    // Get permissions and assigned roles for a specific user.
    [HttpGet("user-permissions/{username}")]
    public async Task<IActionResult> GetUserPermissions(string username)
    {
        var result = await serviceBus.SendQueryAsync<GetUserPermissionsQuery, UserPermissionsDto?>(new GetUserPermissionsQuery(username));

        var events = result is null
            ? EventFactory.CreateFailureEvent(HandlerEventId.ResourceNotFound, $"User '{username}' not found.")
            : EventFactory.CreateSuccessEvent(HandlerEventId.ResourceRetrieved, result);

        return Ok(events);
    }

    // Assign a role and permissions/attributes to any user dynamically.
    // Can assign to MIS-Users, Corporate-Users, or MyBdjobs-Users.
    // Restricted to users who have the 'admin' role in Keycloak.
    [HttpPost("assign-role")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        var events = await serviceBus.SendCommandAsync(new AssignRoleCommand(request));
        return Ok(events);
    }
}
