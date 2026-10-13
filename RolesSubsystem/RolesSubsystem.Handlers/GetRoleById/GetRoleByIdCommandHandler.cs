using RolesSubsystem.DTOs;
using RolesSubsystem.DTOs.Commands;
using RolesSubsystem.Repositories.Abstractions;
using SharedSubsystem.Handlers.Abstractions;

namespace RolesSubsystem.Handlers.GetRoleById;

public class GetRoleByIdCommandHandler : ICommandHandler<GetRoleByIdCommand, RoleResponse?>
{
    private readonly IRoleRepository _roleRepo;

    public GetRoleByIdCommandHandler(IRoleRepository roleRepo)
    {
        _roleRepo = roleRepo;
    }

    public async Task<RoleResponse?> HandleAsync(GetRoleByIdCommand command, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepo.GetByIdAsync(command.Id);
        return role?.ToResponse();
    }
}
