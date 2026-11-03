using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using AuthSubsystem.Handlers.Keycloak;
using SharedSubsystem.Handlers.Abstructions;
using SharedSubsystem.Handlers.Events;

namespace AuthSubsystem.Handlers.Roles;

public class AssignRoleCommandHandler : ICommandHandler<AssignRoleCommand>
{
    private readonly IKeycloakAdminService _keycloakAdminService;

    public AssignRoleCommandHandler(IKeycloakAdminService keycloakAdminService)
    {
        _keycloakAdminService = keycloakAdminService;
    }

    public async Task<IEnumerable<Event>> HandleAsync(AssignRoleCommand command)
    {
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.Username))
        {
            return EventFactory.CreateFailureEvent(EventId.UserInputError, "Username is required.");
        }

        if (string.IsNullOrWhiteSpace(req.Role))
        {
            return EventFactory.CreateFailureEvent(EventId.UserInputError, "Role name is required (e.g. 'admin', 'manager', 'staff', 'recruiter', 'jobseeker').");
        }

        try
        {
            var success = await _keycloakAdminService.AssignRoleAndAttributesAsync(req);
            if (!success)
            {
                return EventFactory.CreateFailureEvent(EventId.InternalServerError, $"Failed to assign role to user '{req.Username}'.");
            }

            var updatedUser = await _keycloakAdminService.GetUserPermissionsByUsernameAsync(req.Username);
            if (updatedUser == null)
            {
                return EventFactory.CreateFailureEvent(EventId.InternalServerError, "Role assigned but failed to fetch updated user.");
            }

            return EventFactory.CreateSuccessEvent(EventId.ResourceUpdated, updatedUser);
        }
        catch (Exception ex)
        {
            return EventFactory.CreateFailureEvent(EventId.InternalServerError, $"An error occurred while assigning role: {ex.Message}");
        }
    }
}
