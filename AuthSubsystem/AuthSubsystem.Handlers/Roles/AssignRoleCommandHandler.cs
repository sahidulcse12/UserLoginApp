using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using AuthSubsystem.Handlers.Keycloak;
using SharedSubsystem.Handlers.Abstractions;

namespace AuthSubsystem.Handlers.Roles;

public class AssignRoleCommandHandler : ICommandHandler<AssignRoleCommand, AuthResult<UserPermissionsDto>>
{
    private readonly IKeycloakAdminService _keycloakAdminService;

    public AssignRoleCommandHandler(IKeycloakAdminService keycloakAdminService)
    {
        _keycloakAdminService = keycloakAdminService;
    }

    public async Task<AuthResult<UserPermissionsDto>> HandleAsync(
        AssignRoleCommand command, CancellationToken cancellationToken = default)
    {
        var req = command.Request;

        if (string.IsNullOrWhiteSpace(req.Username))
        {
            return AuthResult<UserPermissionsDto>.Fail(400, "Username is required.");
        }

        if (string.IsNullOrWhiteSpace(req.Role))
        {
            return AuthResult<UserPermissionsDto>.Fail(400, "Role name is required (e.g. 'admin', 'manager', 'staff', 'recruiter', 'jobseeker').");
        }

        try
        {
            var success = await _keycloakAdminService.AssignRoleAndAttributesAsync(req, cancellationToken);
            if (!success)
            {
                return AuthResult<UserPermissionsDto>.Fail(500, $"Failed to assign role to user '{req.Username}'.");
            }

            var updatedUser = await _keycloakAdminService.GetUserPermissionsByUsernameAsync(req.Username, cancellationToken);
            if (updatedUser == null)
            {
                return AuthResult<UserPermissionsDto>.Fail(500, "Role assigned but failed to fetch updated user.");
            }

            return AuthResult<UserPermissionsDto>.Ok(updatedUser);
        }
        catch (Exception ex)
        {
            return AuthResult<UserPermissionsDto>.Fail(500, $"An error occurred while assigning role: {ex.Message}");
        }
    }
}
