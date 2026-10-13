using RolesSubsystem.Aggregators.Entities;
using RolesSubsystem.DTOs;
using RolesSubsystem.DTOs.Commands;
using RolesSubsystem.Repositories.Abstractions;
using SharedSubsystem.Handlers.Abstractions;

namespace RolesSubsystem.Handlers.CreateRole;

public class CreateRoleCommandHandler : ICommandHandler<CreateRoleCommand, RoleResponse>
{
    private readonly IRoleRepository _roleRepo;
    private readonly IServiceBus _serviceBus;

    public CreateRoleCommandHandler(IRoleRepository roleRepo, IServiceBus serviceBus)
    {
        _roleRepo = roleRepo;
        _serviceBus = serviceBus;
    }

    public async Task<RoleResponse> HandleAsync(CreateRoleCommand command, CancellationToken cancellationToken = default)
    {
        var req = command.Request;
        var role = await _roleRepo.AddAsync(new Role
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Description = req.Description,
            IsActive = true
        });

        await _serviceBus.BroadcastEventAsync(new RoleCreatedEvent(role.Id, role.Name, DateTime.UtcNow), cancellationToken);

        return role.ToResponse();
    }
}
