using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserLoginApp.Api.Data;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Models.Entities;
using UserLoginApp.Api.Services;

namespace UserLoginApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IKeycloakAdminService _keycloakAdminService;

    public AuthController(AppDbContext db, IKeycloakAdminService keycloakAdminService)
    {
        _db = db;
        _keycloakAdminService = keycloakAdminService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and Password are required." });

        var normalizedType = (request.UserType ?? "").Trim().ToLower();
        if (normalizedType != "corporate" && normalizedType != "mybdjobs" && normalizedType != "mis")
            return BadRequest(new { message = "Invalid UserType. Allowed values are 'corporate', 'mybdjobs', or 'mis'." });

        var localExists = await _db.Users.AnyAsync(u => u.Username == request.Username || u.Email == request.Email);
        if (localExists)
            return Conflict(new { message = "User with this username or email already exists in local database." });

        // 1. Create User in Keycloak with userType attribute and assign to Keycloak Group
        string? keycloakId = null;
        try
        {
            keycloakId = await _keycloakAdminService.CreateUserAsync(
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Password,
                normalizedType
            );
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Failed to register user in Keycloak: {ex.Message}" });
        }

        // 2. Save/Sync User in Local AppDbContext (kc.Users table)
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserType = normalizedType,
            KeycloakId = keycloakId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = user.Id,
            keycloakId = user.KeycloakId,
            username = user.Username,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            userType = user.UserType,
            message = "User registered in Keycloak and synced to local database successfully."
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and Password are required." });

        // 1. Authenticate with Keycloak and retrieve Access & Refresh tokens
        var tokenResponse = await _keycloakAdminService.LoginUserAsync(request.Username, request.Password);
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            return Unauthorized(new { message = "Invalid credentials or Keycloak authentication failed." });

        // 2. Fetch local user profile from database (kc.Users)
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Username);
        if (user != null)
        {
            user.RefreshToken = tokenResponse.RefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            tokenResponse.User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserType = user.UserType,
                KeycloakId = user.KeycloakId
            };
        }

        return Ok(tokenResponse);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { message = "RefreshToken is required." });

        var tokenResponse = await _keycloakAdminService.RefreshTokenAsync(request.RefreshToken);
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        return Ok(tokenResponse);
    }
}
