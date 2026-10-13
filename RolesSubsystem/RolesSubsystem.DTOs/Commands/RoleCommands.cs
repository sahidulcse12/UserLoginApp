using SharedSubsystem.DTOs.Abstractions;

namespace RolesSubsystem.DTOs.Commands;

// ── Commands ─────────────────────────────────────────────────────────────

public record GetRolesCommand : ICommand<List<RoleResponse>>;

public record GetRoleByIdCommand(Guid Id) : ICommand<RoleResponse?>;

public record CreateRoleCommand(CreateRoleRequest Request) : ICommand<RoleResponse>;

// ── Events ───────────────────────────────────────────────────────────────

public record RoleCreatedEvent(
    Guid RoleId,
    string RoleName,
    DateTime OccurredOn
) : IEvent;
