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
        // Already enriched in this request cycle
        if (principal.HasClaim(c => c.Type == "feature"))
            return principal;

        var keycloakId = principal.FindFirst("sub")?.Value;
        var preferredUsername = principal.FindFirst("preferred_username")?.Value;

        if (string.IsNullOrEmpty(keycloakId) && string.IsNullOrEmpty(preferredUsername))
            return principal;

        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RoleFeatures)
                        .ThenInclude(rf => rf.Feature)
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
                IsActive = true
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

        var features = user.UserRoles
            .SelectMany(ur => ur.Role.RoleFeatures)
            .Select(rf => rf.Feature.Code)
            .Distinct();

        foreach (var feature in features)
            identity.AddClaim(new Claim("feature", feature));

        principal.AddIdentity(identity);
        return principal;
    }
}
