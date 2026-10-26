using SharedSubsystem.DTOs.Abstractions;

namespace AuthSubsystem.DTOs.Commands;

public record GetAllUsersWithRolesQuery(string? UserType = null) : ICommand<AuthResult<IEnumerable<KeycloakUserRoleDto>>>;

public record GetUserPermissionsQuery(string Username) : ICommand<AuthResult<UserPermissionsDto>>;

public record AssignRoleCommand(AssignRoleRequest Request) : ICommand<AuthResult<UserPermissionsDto>>;
