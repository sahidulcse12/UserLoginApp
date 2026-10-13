using RolesSubsystem.DTOs;
using RolesSubsystem.DTOs.Commands;
using RolesSubsystem.Repositories.Abstractions;
using SharedSubsystem.Handlers.Abstractions;

namespace RolesSubsystem.Handlers.GetRoles;

public class GetRolesCommandHandler : ICommandHandler<GetRolesCommand, List<RoleResponse>>
{
    private readonly IRoleRepository _roleRepo;

    public GetRolesCommandHandler(IRoleRepository roleRepo)
    {
        _roleRepo = roleRepo;
    }

    public async Task<List<RoleResponse>> HandleAsync(GetRolesCommand command, CancellationToken cancellationToken = default)
    {
        var roles = await _roleRepo.GetAllAsync();
        return roles.Select(r => r.ToResponse()).ToList();
    }
}
