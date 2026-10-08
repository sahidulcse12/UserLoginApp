using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Models.Entities;

namespace UserLoginApp.Api.Data.Repositories;

public interface IAuthRepository
{
    Task<bool> UserExistsAsync(string username, string email);
    Task<User?> GetUserByUsernameOrEmailAsync(string identifier);
    Task<User?> GetUserByUsernameOrEmailExactAsync(string username, string email);
    Task<User> AddUserAsync(User user);
    Task UpdateUserAsync(User user);
    Task<LegacyAdminUserDto?> GetLegacyAdminUserAsync(string usernameInput, string passwordInput);
}
