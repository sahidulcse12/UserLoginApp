using RolesSubsystem.Aggregators.Entities;
using RolesSubsystem.DTOs;

namespace RolesSubsystem.Handlers;

internal static class RoleMapping
{
    public static RoleResponse ToResponse(this Role role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        IsActive = role.IsActive,
        Features = role.RoleFeatures.Select(rf => rf.Feature.Name).ToList()
    };
}
