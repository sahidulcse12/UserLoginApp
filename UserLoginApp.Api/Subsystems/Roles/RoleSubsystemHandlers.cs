using UserLoginApp.Api.Core.Messaging;
using UserLoginApp.Api.Models.DTOs;

namespace UserLoginApp.Api.Subsystems.Roles;

public class GetRolesCommandHandler : ICommandHandler<GetRolesCommand, List<RoleResponse>>
{
    public Task<List<RoleResponse>> HandleAsync(GetRolesCommand command, CancellationToken cancellationToken = default)
    {
        var defaultRoles = new List<RoleResponse>
        {
            new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Corporate-Users", Description = "Default role for Corporate employers", IsActive = true, Features = new() { "JobPosting", "ApplicantTracking" } },
            new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "MyBdjobs-Users", Description = "Default role for Job seekers", IsActive = true, Features = new() { "ProfileManagement", "JobApplication" } },
            new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "MIS-Users", Description = "Administrative role for internal operations", IsActive = true, Features = new() { "UserManagement", "SystemAudit" } }
        };

        return Task.FromResult(defaultRoles);
    }
}

public class GetRoleByIdCommandHandler : ICommandHandler<GetRoleByIdCommand, RoleResponse?>
{
    public Task<RoleResponse?> HandleAsync(GetRoleByIdCommand command, CancellationToken cancellationToken = default)
    {
        var id = command.Id;
        if (id == Guid.Parse("11111111-1111-1111-1111-111111111111"))
            return Task.FromResult<RoleResponse?>(new() { Id = id, Name = "Corporate-Users", Description = "Default role for Corporate employers", IsActive = true });

        if (id == Guid.Parse("22222222-2222-2222-2222-222222222222"))
            return Task.FromResult<RoleResponse?>(new() { Id = id, Name = "MyBdjobs-Users", Description = "Default role for Job seekers", IsActive = true });

        if (id == Guid.Parse("33333333-3333-3333-3333-333333333333"))
            return Task.FromResult<RoleResponse?>(new() { Id = id, Name = "MIS-Users", Description = "Administrative role for internal operations", IsActive = true });

        return Task.FromResult<RoleResponse?>(null);
    }
}

public class CreateRoleCommandHandler : ICommandHandler<CreateRoleCommand, RoleResponse>
{
    private readonly IServiceBus _serviceBus;

    public CreateRoleCommandHandler(IServiceBus serviceBus)
    {
        _serviceBus = serviceBus;
    }

    public async Task<RoleResponse> HandleAsync(CreateRoleCommand command, CancellationToken cancellationToken = default)
    {
        var req = command.Request;
        var newRole = new RoleResponse
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Description = req.Description,
            IsActive = true
        };

        await _serviceBus.BroadcastEventAsync(new RoleCreatedEvent(newRole.Id, newRole.Name, DateTime.UtcNow), cancellationToken);

        return newRole;
    }
}
