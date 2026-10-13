using UserLoginApp.Api.Core.Messaging;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Subsystems.Roles;

namespace UserLoginApp.Api.Orchestrators;

public interface IRoleOrchestrator
{
    Task<List<RoleResponse>> GetAllRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleResponse?> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
}

public class RoleOrchestrator : IRoleOrchestrator
{
    private readonly IServiceBus _serviceBus;
    private readonly ILogger<RoleOrchestrator> _logger;

    public RoleOrchestrator(IServiceBus serviceBus, ILogger<RoleOrchestrator> logger)
    {
        _serviceBus = serviceBus;
        _logger = logger;
    }

    public async Task<List<RoleResponse>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[RoleOrchestrator] Dispatching GetRolesCommand");
        return await _serviceBus.SendCommandAsync(new GetRolesCommand(), cancellationToken);
    }

    public async Task<RoleResponse?> GetRoleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[RoleOrchestrator] Dispatching GetRoleByIdCommand for {RoleId}", id);
        return await _serviceBus.SendCommandAsync(new GetRoleByIdCommand(id), cancellationToken);
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[RoleOrchestrator] Dispatching CreateRoleCommand for '{RoleName}'", request.Name);
        return await _serviceBus.SendCommandAsync(new CreateRoleCommand(request), cancellationToken);
    }
}
