using SharedSubsystem.DTOs.Abstractions;

namespace AuthSubsystem.DTOs.Commands;

public record GetAllUsersWithRolesQuery(string? UserType = null) : IQuery<IEnumerable<KeycloakUserRoleDto>>;

public record GetUserPermissionsQuery(string Username) : IQuery<UserPermissionsDto?>;

public record AssignRoleCommand(AssignRoleRequest Request) : ICommand;
