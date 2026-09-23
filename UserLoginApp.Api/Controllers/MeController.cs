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
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RoleFeatures)
                        .ThenInclude(rf => rf.Feature)
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId || (!string.IsNullOrEmpty(preferredUsername) && u.Username == preferredUsername));

        if (user == null)
            return NotFound(new { message = "User not found in application database." });

        var features = user.UserRoles
            .SelectMany(ur => ur.Role.RoleFeatures)
            .Select(rf => rf.Feature.Code)
            .Distinct()
            .ToList();

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            userType = user.UserType,
            roles,
            features
        });
    }
}
