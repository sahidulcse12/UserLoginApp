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

    /// <summary>
    /// Registers a user both in Keycloak (setting userType attribute) and syncs to local AppDbContext.
    /// User types allowed: 'mybdjobs' or 'corporate'
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and Password are required." });

        var normalizedType = (request.UserType ?? "").Trim().ToLower();
        if (normalizedType != "mybdjobs" && normalizedType != "corporate")
            return BadRequest(new { message = "Invalid UserType. Allowed values are 'mybdjobs' or 'corporate'." });

        var localExists = await _db.Users.AnyAsync(u => u.Username == request.Username || u.Email == request.Email);
        if (localExists)
            return Conflict(new { message = "User with this username or email already exists in local database." });

        // 1. Create User in Keycloak with userType attribute
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

        // 2. Save/Sync User in Local AppDbContext
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserType = normalizedType,
            KeycloakId = keycloakId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Assign default role (MyBdJobsUser or CorporateUser)
        var defaultRoleName = normalizedType == "corporate" ? "CorporateUser" : "MyBdJobsUser";
        var defaultRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == defaultRoleName || r.Name == "User");
        if (defaultRole != null)
        {
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = defaultRole.Id });
            await _db.SaveChangesAsync();
        }

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
}
