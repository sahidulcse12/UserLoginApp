using SharedSubsystem.DTOs.Abstractions;

namespace AuthSubsystem.DTOs.Commands;

public record GetAllUsersWithRolesQuery(string? UserType = null) : IQuery<AuthResult<IEnumerable<KeycloakUserRoleDto>>>;

public record GetUserPermissionsQuery(string Username) : IQuery<AuthResult<UserPermissionsDto>>;

public record AssignRoleCommand(AssignRoleRequest Request) : ICommand<AuthResult<UserPermissionsDto>>;
