using RolesSubsystem.Aggregators.Entities;
using RolesSubsystem.Repositories.Abstractions;

namespace RolesSubsystem.Repositories;

// The live DB only contains kc.Users, so roles are served from these defaults until role tables exist.
public class RoleRepository : IRoleRepository
{
    private static readonly List<Role> DefaultRoles = new()
    {
        CreateRole("11111111-1111-1111-1111-111111111111", "Corporate-Users", "Default role for Corporate employers", "JobPosting", "ApplicantTracking"),
        CreateRole("22222222-2222-2222-2222-222222222222", "MyBdjobs-Users", "Default role for Job seekers", "ProfileManagement", "JobApplication"),
        CreateRole("33333333-3333-3333-3333-333333333333", "MIS-Users", "Administrative role for internal operations", "UserManagement", "SystemAudit")
    };

    public Task<List<Role>> GetAllAsync() => Task.FromResult(DefaultRoles.ToList());

    public Task<Role?> GetByIdAsync(Guid id) => Task.FromResult(DefaultRoles.FirstOrDefault(r => r.Id == id));

    // Not persisted yet: there is no role table in the live DB.
    public Task<Role> AddAsync(Role role) => Task.FromResult(role);

    private static Role CreateRole(string id, string name, string description, params string[] features)
    {
        var role = new Role { Id = Guid.Parse(id), Name = name, Description = description, IsActive = true };
        foreach (var feature in features)
        {
            role.RoleFeatures.Add(new RoleFeature
            {
                RoleId = role.Id,
                Role = role,
                Feature = new Feature { Name = feature, Code = feature }
            });
        }
        return role;
    }
}
