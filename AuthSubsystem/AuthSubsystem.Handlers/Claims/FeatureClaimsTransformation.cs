using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using AuthSubsystem.Aggregators.Entities;
using AuthSubsystem.Repositories.Abstractions;

namespace AuthSubsystem.Handlers.Claims;

public class FeatureClaimsTransformation : IClaimsTransformation
{
    private readonly IAuthRepository _authRepo;

    public FeatureClaimsTransformation(IAuthRepository authRepo) => _authRepo = authRepo;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(c => c.Type == "userType"))
            return principal;

        var keycloakId = principal.FindFirst("sub")?.Value;
        var preferredUsername = principal.FindFirst("preferred_username")?.Value;

        if (string.IsNullOrEmpty(keycloakId) && string.IsNullOrEmpty(preferredUsername))
            return principal;

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

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim("userType", user.UserType));
        principal.AddIdentity(identity);

        return principal;
    }
}
