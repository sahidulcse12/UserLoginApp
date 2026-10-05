using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserLoginApp.Api.Data;

namespace UserLoginApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly AppDbContext _db;

    public MeController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetMe()
    {
        var keycloakId = User.FindFirst("sub")?.Value;
        var preferredUsername = User.FindFirst("preferred_username")?.Value;

        if (string.IsNullOrEmpty(keycloakId) && string.IsNullOrEmpty(preferredUsername))
            return Unauthorized();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId || (!string.IsNullOrEmpty(preferredUsername) && u.Username == preferredUsername));

        if (user == null)
            return NotFound(new { message = "User not found in application database." });

        return Ok(new
        {
            id = user.Id,
            keycloakId = user.KeycloakId,
            username = user.Username,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            userType = user.UserType,
            isActive = user.IsActive,
            createdAt = user.CreatedAt,
            updatedAt = user.UpdatedAt
        });
    }
}
