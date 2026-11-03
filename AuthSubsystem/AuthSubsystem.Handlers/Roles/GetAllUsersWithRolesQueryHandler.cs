using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using AuthSubsystem.Handlers.Keycloak;
using SharedSubsystem.Handlers.Abstructions;

namespace AuthSubsystem.Handlers.Roles;

public class GetAllUsersWithRolesQueryHandler : IQueryHandler<GetAllUsersWithRolesQuery, IEnumerable<KeycloakUserRoleDto>>
{
    private readonly IKeycloakAdminService _keycloakAdminService;

    public GetAllUsersWithRolesQueryHandler(IKeycloakAdminService keycloakAdminService)
    {
        _keycloakAdminService = keycloakAdminService;
    }

    public async Task<IEnumerable<KeycloakUserRoleDto>> HandleAsync(GetAllUsersWithRolesQuery query)
    {
        return await _keycloakAdminService.GetAllUsersWithRolesAsync(query.UserType);
    }
}
