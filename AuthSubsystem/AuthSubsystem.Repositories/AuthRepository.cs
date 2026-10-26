using System.Data;
using Microsoft.EntityFrameworkCore;
using AuthSubsystem.DTOs;
using AuthSubsystem.Aggregators.Entities;
using AuthSubsystem.Repositories.Abstractions;
using AuthSubsystem.Repositories.Data;
using Microsoft.Extensions.Logging;

namespace AuthSubsystem.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuthRepository> _logger;

    public AuthRepository(AppDbContext db, ILogger<AuthRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> UserExistsAsync(string username, string email)
    {
        return await _db.Users.AnyAsync(u => u.Username == username || u.Email == email);
    }

    public async Task<User?> GetUserByUsernameOrEmailAsync(string identifier)
    {
        var normalized = identifier.Trim().ToLower();
        return await _db.Users.FirstOrDefaultAsync(u => 
            u.Username.ToLower() == normalized || 
            (u.Email != null && u.Email.ToLower() == normalized));
    }

    public async Task<User?> GetUserByUsernameOrEmailExactAsync(string username, string email)
    {
        return await _db.Users.FirstOrDefaultAsync(u => 
            u.Username == username || 
            u.Email == username || 
            u.Email == email);
    }

    public async Task<User> AddUserAsync(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task UpdateUserAsync(User user)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    public async Task<User?> GetActiveUserByKeycloakIdOrUsernameAsync(string? keycloakId, string? username)
    {
        return await _db.Users.FirstOrDefaultAsync(u =>
            (u.KeycloakId == keycloakId || (!string.IsNullOrEmpty(username) && u.Username == username)) && u.IsActive);
    }

    public async Task<User?> GetUserByRefreshTokenAsync(string refreshToken)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
    }

    public async Task<LegacyAdminUserDto?> GetLegacyAdminUserAsync(string usernameInput, string passwordInput)
    {
        try
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT TOP 1 USER_NAME, E_mail, ViewAccess, ModuleAccess, SubLinkAccess, DepId, AccessFromHome, AdminChk
                FROM [dbo].[ADMIN] 
                WHERE (LOWER(RTRIM(LTRIM(USER_NAME))) = LOWER(@user) OR LOWER(RTRIM(LTRIM(E_mail))) = LOWER(@user))
                  AND (RTRIM(LTRIM(PASSWORD)) = @pass OR RTRIM(LTRIM(UPASS)) = @pass)
                  AND (Deleted IS NULL OR Deleted = 0)";

            var pUser = cmd.CreateParameter();
            pUser.ParameterName = "@user";
            pUser.Value = usernameInput.Trim();
            cmd.Parameters.Add(pUser);

            var pPass = cmd.CreateParameter();
            pPass.ParameterName = "@pass";
            pPass.Value = passwordInput;
            cmd.Parameters.Add(pPass);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var dbUsername = reader.IsDBNull(0) ? usernameInput.Trim() : reader.GetString(0).Trim();
                var dbEmail = reader.IsDBNull(1) ? $"{dbUsername}@bdjobs.com" : reader.GetString(1).Trim();
                if (string.IsNullOrWhiteSpace(dbEmail))
                {
                    dbEmail = $"{dbUsername}@bdjobs.com";
                }

                var viewAccess = reader.IsDBNull(2) ? null : reader.GetString(2)?.Trim();
                var moduleAccess = reader.IsDBNull(3) ? null : reader.GetString(3)?.Trim();
                var subLinkAccess = reader.IsDBNull(4) ? null : reader.GetString(4)?.Trim();
                int? depId = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5));
                int? accessFromHome = reader.IsDBNull(6) ? null : Convert.ToInt32(reader.GetValue(6));
                int adminChk = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7));

                return new LegacyAdminUserDto
                {
                    Username = dbUsername,
                    Email = dbEmail,
                    ViewAccess = viewAccess,
                    ModuleAccess = moduleAccess,
                    SubLinkAccess = subLinkAccess,
                    DepId = depId,
                    AccessFromHome = accessFromHome,
                    AdminChk = adminChk
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying legacy dbo.ADMIN table for user '{Username}'", usernameInput);
        }

        return null;
    }
}
