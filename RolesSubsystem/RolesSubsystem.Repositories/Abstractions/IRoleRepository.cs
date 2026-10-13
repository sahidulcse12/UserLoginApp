using RolesSubsystem.Aggregators.Entities;

namespace RolesSubsystem.Repositories.Abstractions;

public interface IRoleRepository
{
    Task<List<Role>> GetAllAsync();
    Task<Role?> GetByIdAsync(Guid id);
    Task<Role> AddAsync(Role role);
}
