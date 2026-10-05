using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UserLoginApp.Api.Data;

namespace UserLoginApp.Api.Auth;

public class FeatureClaimsTransformation : IClaimsTransformation
{
    private readonly AppDbContext _db;

    public FeatureClaimsTransformation(AppDbContext db) => _db = db;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(c => c.Type == "userType"))
            return principal;

        var keycloakId = principal.FindFirst("sub")?.Value;
        var preferredUsername = principal.FindFirst("preferred_username")?.Value;

        if (string.IsNullOrEmpty(keycloakId) && string.IsNullOrEmpty(preferredUsername))
            return principal;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => (u.KeycloakId == keycloakId || (!string.IsNullOrEmpty(preferredUsername) && u.Username == preferredUsername)) && u.IsActive);

        if (user == null)
        {
            var email = principal.FindFirst("email")?.Value ?? $"{preferredUsername ?? keycloakId}@keycloak.local";
            var userType = principal.FindFirst("userType")?.Value ?? "mybdjobs";

            user = new Models.Entities.User
            {
                Username = preferredUsername ?? keycloakId ?? Guid.NewGuid().ToString(),
                Email = email,
                KeycloakId = keycloakId,
                UserType = userType,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else if (!string.IsNullOrEmpty(keycloakId) && user.KeycloakId != keycloakId)
        {
            user.KeycloakId = keycloakId;
            await _db.SaveChangesAsync();
        }

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("userType", user.UserType));
        principal.AddIdentity(identity);

        return principal;
    }
}
