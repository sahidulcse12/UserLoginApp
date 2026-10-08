using System.Data;
using Microsoft.EntityFrameworkCore;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Models.Entities;

namespace UserLoginApp.Api.Data.Repositories;

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
                SELECT TOP 1 USER_NAME, E_mail 
                FROM [dbo].[ADMIN] 
                WHERE (LOWER(RTRIM(LTRIM(USER_NAME))) = LOWER(@user) OR LOWER(RTRIM(LTRIM(E_mail))) = LOWER(@user))
                  AND (PASSWORD = @pass OR UPASS = @pass)
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

                return new LegacyAdminUserDto
                {
                    Username = dbUsername,
                    Email = dbEmail
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
