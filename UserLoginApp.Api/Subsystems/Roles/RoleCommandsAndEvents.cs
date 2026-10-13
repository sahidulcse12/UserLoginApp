using UserLoginApp.Api.Core.Messaging;
using UserLoginApp.Api.Models.DTOs;

namespace UserLoginApp.Api.Subsystems.Roles;

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
