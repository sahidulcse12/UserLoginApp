using Microsoft.EntityFrameworkCore;
using UserLoginApp.Api.Data;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Models.Entities;

namespace UserLoginApp.Api.Services;

public interface IUserService
{
    Task<List<UserResponse>> GetAllAsync();
    Task<UserResponse?> GetByIdAsync(Guid id);
    Task<UserResponse> CreateAsync(CreateUserRequest request);
    Task<UserResponse?> UpdateAsync(Guid id, UpdateUserRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> SetActiveAsync(Guid id, bool isActive);
    Task<bool> AssignRoleAsync(Guid userId, Guid roleId);
    Task<bool> RemoveRoleAsync(Guid userId, Guid roleId);
}

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly IKeycloakAdminService _keycloak;

    public UserService(AppDbContext db, IKeycloakAdminService keycloak)
    {
        _db = db;
        _keycloak = keycloak;
    }

    public async Task<List<UserResponse>> GetAllAsync()
    {
        return await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Select(u => MapToResponse(u))
            .ToListAsync();
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
        return user == null ? null : MapToResponse(user);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request)
    {
        string? keycloakId = null;
        var userType = string.Equals(request.UserType, "corporate", StringComparison.OrdinalIgnoreCase) ? "corporate" : "mybdjobs";
        try
        {
            keycloakId = await _keycloak.CreateUserAsync(
                request.Username, request.Email,
                request.FirstName, request.LastName,
                request.Password, userType);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UserService] Keycloak creation warning: {ex.Message}");
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserType = userType,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            KeycloakId = keycloakId
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return MapToResponse(user);
    }

    public async Task<UserResponse?> UpdateAsync(Guid id, UpdateUserRequest request)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return null;

        // Sync to Keycloak if linked
        if (!string.IsNullOrEmpty(user.KeycloakId))
        {
            try
            {
                await _keycloak.UpdateUserAsync(user.KeycloakId, request.Email, request.FirstName, request.LastName);

                if (!string.IsNullOrWhiteSpace(request.NewPassword))
                    await _keycloak.ResetPasswordAsync(user.KeycloakId, request.NewPassword);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserService] Keycloak update warning: {ex.Message}");
            }
        }

        user.Email = request.Email;
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        await _db.SaveChangesAsync();
        return MapToResponse(user);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return false;

        if (!string.IsNullOrEmpty(user.KeycloakId))
        {
            try
            {
                await _keycloak.DeleteUserAsync(user.KeycloakId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserService] Keycloak delete warning: {ex.Message}");
            }
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return false;

        if (!string.IsNullOrEmpty(user.KeycloakId))
        {
            try
            {
                await _keycloak.SetEnabledAsync(user.KeycloakId, isActive);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserService] Keycloak SetActive warning: {ex.Message}");
            }
        }

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AssignRoleAsync(Guid userId, Guid roleId)
    {
        var exists = await _db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (exists) return true;

        var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
        var roleExists = await _db.Roles.AnyAsync(r => r.Id == roleId);
        if (!userExists || !roleExists) return false;

        _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveRoleAsync(Guid userId, Guid roleId)
    {
        var userRole = await _db.UserRoles.FindAsync(userId, roleId);
        if (userRole == null) return false;
        _db.UserRoles.Remove(userRole);
        await _db.SaveChangesAsync();
        return true;
    }

    private static UserResponse MapToResponse(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        FirstName = u.FirstName,
        LastName = u.LastName,
        UserType = u.UserType,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt,
        Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList()
    };
}
