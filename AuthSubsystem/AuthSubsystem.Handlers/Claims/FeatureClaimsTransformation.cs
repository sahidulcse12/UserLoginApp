using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text.Json;
using AuthSubsystem.Aggregators.Entities;
using AuthSubsystem.Repositories.Abstractions;

namespace AuthSubsystem.Handlers.Claims;

public class FeatureClaimsTransformation : IClaimsTransformation
{
    private readonly IAuthRepository _authRepo;

    public FeatureClaimsTransformation(IAuthRepository authRepo) => _authRepo = authRepo;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var keycloakId = principal.FindFirst("sub")?.Value;
        var preferredUsername = principal.FindFirst("preferred_username")?.Value;

        var identity = new ClaimsIdentity();

        // 1. Map Keycloak Realm Roles from "realm_access" claim into standard Role claims
        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (!string.IsNullOrEmpty(realmAccess))
        {
            try
            {
                using var doc = JsonDocument.Parse(realmAccess);
                if (doc.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
                {
                    foreach (var role in roles.EnumerateArray())
                    {
                        var roleName = role.GetString();
                        if (!string.IsNullOrEmpty(roleName) && !principal.IsInRole(roleName))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                            identity.AddClaim(new Claim("role", roleName));
                        }
                    }
                }
            }
            catch { /* ignore json parse errors on malformed claims */ }
        }

        // 2. Map direct "roles" or "role" claims if present
        foreach (var rClaim in principal.FindAll("roles"))
        {
            if (!principal.IsInRole(rClaim.Value))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, rClaim.Value));
            }
        }

        // 3. Sync or create user locally for userType claim
        if (!principal.HasClaim(c => c.Type == "userType") &&
            (!string.IsNullOrEmpty(keycloakId) || !string.IsNullOrEmpty(preferredUsername)))
        {
            var user = await _authRepo.GetActiveUserByKeycloakIdOrUsernameAsync(keycloakId, preferredUsername);

            if (user == null)
            {
                var email = principal.FindFirst("email")?.Value ?? $"{preferredUsername ?? keycloakId}@keycloak.local";
                var userType = principal.FindFirst("userType")?.Value ?? "mybdjobs";

                user = new User
                {
                    Username = preferredUsername ?? keycloakId ?? Guid.NewGuid().ToString(),
                    Email = email,
                    KeycloakId = keycloakId,
                    UserType = userType,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _authRepo.AddUserAsync(user);
            }
            else if (!string.IsNullOrEmpty(keycloakId) && user.KeycloakId != keycloakId)
            {
                user.KeycloakId = keycloakId;
                await _authRepo.UpdateUserAsync(user);
            }

            identity.AddClaim(new Claim("userType", user.UserType));
        }

        if (identity.Claims.Any())
        {
            principal.AddIdentity(identity);
        }

        return principal;
    }
}
