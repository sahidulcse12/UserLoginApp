using AuthSubsystem.DTOs;
using AuthSubsystem.Aggregators.Entities;

namespace AuthSubsystem.Repositories.Abstractions;

public interface IAuthRepository
{
    Task<bool> UserExistsAsync(string username, string email);
    Task<User?> GetUserByUsernameOrEmailAsync(string identifier);
    Task<User?> GetUserByUsernameOrEmailExactAsync(string username, string email);
    Task<User> AddUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task<User?> GetActiveUserByKeycloakIdOrUsernameAsync(string? keycloakId, string? username);
    Task<User?> GetUserByRefreshTokenAsync(string refreshToken);
    Task<LegacyAdminUserDto?> GetLegacyAdminUserAsync(string usernameInput, string passwordInput);
}
