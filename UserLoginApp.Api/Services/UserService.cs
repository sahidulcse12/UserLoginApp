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
            .Select(u => MapToResponse(u))
            .ToListAsync();
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == id);
        return user == null ? null : MapToResponse(user);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request)
    {
        string? keycloakId = null;
        var userType = (request.UserType ?? "").Trim().ToLower() switch
        {
            "corporate" => "corporate",
            "mis" => "mis",
            _ => "mybdjobs"
        };

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
            KeycloakId = keycloakId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return MapToResponse(user);
    }

    public async Task<UserResponse?> UpdateAsync(Guid id, UpdateUserRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return null;

        var newUserType = !string.IsNullOrWhiteSpace(request.UserType)
            ? (request.UserType.Trim().ToLower() switch
              {
                  "corporate" => "corporate",
                  "mis" => "mis",
                  _ => "mybdjobs"
              })
            : user.UserType;

        user.Email = request.Email;
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.UserType = newUserType;
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

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return false;

        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
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
        Roles = new List<string>()
    };
}
