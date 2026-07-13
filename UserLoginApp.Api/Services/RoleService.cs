using Microsoft.EntityFrameworkCore;
using UserLoginApp.Api.Data;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Models.Entities;

namespace UserLoginApp.Api.Services;

public interface IRoleService
{
    Task<List<RoleResponse>> GetAllAsync();
    Task<RoleResponse?> GetByIdAsync(Guid id);
    Task<RoleResponse> CreateAsync(CreateRoleRequest request);
    Task<RoleResponse?> UpdateAsync(Guid id, UpdateRoleRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<List<FeatureResponse>> GetAllFeaturesAsync();
    Task<bool> AssignFeatureAsync(Guid roleId, Guid featureId);
    Task<bool> RemoveFeatureAsync(Guid roleId, Guid featureId);
}

public class RoleService : IRoleService
{
    private readonly AppDbContext _db;

    public RoleService(AppDbContext db) => _db = db;

    public async Task<List<RoleResponse>> GetAllAsync()
    {
        return await _db.Roles
            .Include(r => r.RoleFeatures).ThenInclude(rf => rf.Feature)
            .Select(r => MapToResponse(r))
            .ToListAsync();
    }

    public async Task<RoleResponse?> GetByIdAsync(Guid id)
    {
        var role = await _db.Roles
            .Include(r => r.RoleFeatures).ThenInclude(rf => rf.Feature)
            .FirstOrDefaultAsync(r => r.Id == id);
        return role == null ? null : MapToResponse(role);
    }

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request)
    {
        var role = new Role { Name = request.Name, Description = request.Description };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return MapToResponse(role);
    }

    public async Task<RoleResponse?> UpdateAsync(Guid id, UpdateRoleRequest request)
    {
        var role = await _db.Roles
            .Include(r => r.RoleFeatures).ThenInclude(rf => rf.Feature)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return null;

        role.Name = request.Name;
        role.Description = request.Description;
        await _db.SaveChangesAsync();
        return MapToResponse(role);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return false;
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<FeatureResponse>> GetAllFeaturesAsync()
    {
        return await _db.Features
            .Select(f => new FeatureResponse { Id = f.Id, Name = f.Name, Code = f.Code, Description = f.Description })
            .ToListAsync();
    }

    public async Task<bool> AssignFeatureAsync(Guid roleId, Guid featureId)
    {
        var exists = await _db.RoleFeatures.AnyAsync(rf => rf.RoleId == roleId && rf.FeatureId == featureId);
        if (exists) return true;

        var roleExists = await _db.Roles.AnyAsync(r => r.Id == roleId);
        var featureExists = await _db.Features.AnyAsync(f => f.Id == featureId);
        if (!roleExists || !featureExists) return false;

        _db.RoleFeatures.Add(new RoleFeature { RoleId = roleId, FeatureId = featureId });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFeatureAsync(Guid roleId, Guid featureId)
    {
        var rf = await _db.RoleFeatures.FindAsync(roleId, featureId);
        if (rf == null) return false;
        _db.RoleFeatures.Remove(rf);
        await _db.SaveChangesAsync();
        return true;
    }

    private static RoleResponse MapToResponse(Role r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        IsActive = r.IsActive,
        Features = r.RoleFeatures.Select(rf => rf.Feature.Code).ToList()
    };
}
