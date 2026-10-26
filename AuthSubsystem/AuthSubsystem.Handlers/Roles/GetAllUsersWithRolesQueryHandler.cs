using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using AuthSubsystem.Handlers.Keycloak;
using SharedSubsystem.Handlers.Abstractions;

namespace AuthSubsystem.Handlers.Roles;

public class GetAllUsersWithRolesQueryHandler : ICommandHandler<GetAllUsersWithRolesQuery, AuthResult<IEnumerable<KeycloakUserRoleDto>>>
{
    private readonly IKeycloakAdminService _keycloakAdminService;

    public GetAllUsersWithRolesQueryHandler(IKeycloakAdminService keycloakAdminService)
    {
        _keycloakAdminService = keycloakAdminService;
    }

    public async Task<AuthResult<IEnumerable<KeycloakUserRoleDto>>> HandleAsync(
        GetAllUsersWithRolesQuery command, CancellationToken cancellationToken = default)
    {
        try
        {
            var users = await _keycloakAdminService.GetAllUsersWithRolesAsync(command.UserType, cancellationToken);
            return AuthResult<IEnumerable<KeycloakUserRoleDto>>.Ok(users);
        }
        catch (Exception ex)
        {
            return AuthResult<IEnumerable<KeycloakUserRoleDto>>.Fail(500, $"Failed to retrieve users: {ex.Message}");
        }
    }
}
