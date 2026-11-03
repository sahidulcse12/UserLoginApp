using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using AuthSubsystem.Handlers.Keycloak;
using SharedSubsystem.Handlers.Abstructions;

namespace AuthSubsystem.Handlers.Roles;

public class GetUserPermissionsQueryHandler : IQueryHandler<GetUserPermissionsQuery, UserPermissionsDto?>
{
    private readonly IKeycloakAdminService _keycloakAdminService;

    public GetUserPermissionsQueryHandler(IKeycloakAdminService keycloakAdminService)
    {
        _keycloakAdminService = keycloakAdminService;
    }

    public async Task<UserPermissionsDto?> HandleAsync(GetUserPermissionsQuery query)
    {
        return await _keycloakAdminService.GetUserPermissionsByUsernameAsync(query.Username);
    }
}
