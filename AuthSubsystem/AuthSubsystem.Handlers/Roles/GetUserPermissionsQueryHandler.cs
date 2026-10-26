using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using AuthSubsystem.Handlers.Keycloak;
using SharedSubsystem.Handlers.Abstractions;

namespace AuthSubsystem.Handlers.Roles;

public class GetUserPermissionsQueryHandler : ICommandHandler<GetUserPermissionsQuery, AuthResult<UserPermissionsDto>>
{
    private readonly IKeycloakAdminService _keycloakAdminService;

    public GetUserPermissionsQueryHandler(IKeycloakAdminService keycloakAdminService)
    {
        _keycloakAdminService = keycloakAdminService;
    }

    public async Task<AuthResult<UserPermissionsDto>> HandleAsync(
        GetUserPermissionsQuery command, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _keycloakAdminService.GetUserPermissionsByUsernameAsync(command.Username, cancellationToken);
            if (user == null)
            {
                return AuthResult<UserPermissionsDto>.Fail(404, $"User '{command.Username}' not found.");
            }

            return AuthResult<UserPermissionsDto>.Ok(user);
        }
        catch (Exception ex)
        {
            return AuthResult<UserPermissionsDto>.Fail(500, $"Failed to retrieve permissions: {ex.Message}");
        }
    }
}
