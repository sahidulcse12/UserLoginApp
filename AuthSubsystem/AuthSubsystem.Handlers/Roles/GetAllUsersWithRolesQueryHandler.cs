using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using AuthSubsystem.Handlers.Keycloak;
using SharedSubsystem.Handlers.Abstructions;

namespace AuthSubsystem.Handlers.Roles;

public class GetAllUsersWithRolesQueryHandler : IQueryHandler<GetAllUsersWithRolesQuery, AuthResult<IEnumerable<KeycloakUserRoleDto>>>
{
    private readonly IKeycloakAdminService _keycloakAdminService;

    public GetAllUsersWithRolesQueryHandler(IKeycloakAdminService keycloakAdminService)
    {
        _keycloakAdminService = keycloakAdminService;
    }

    public async Task<AuthResult<IEnumerable<KeycloakUserRoleDto>>> HandleAsync(GetAllUsersWithRolesQuery query)
    {
        try
        {
            var users = await _keycloakAdminService.GetAllUsersWithRolesAsync(query.UserType);
            return AuthResult<IEnumerable<KeycloakUserRoleDto>>.Ok(users);
        }
        catch (Exception ex)
        {
            return AuthResult<IEnumerable<KeycloakUserRoleDto>>.Fail(500, $"Failed to retrieve users: {ex.Message}");
        }
    }
}
