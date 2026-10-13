using System.Text;
using System.Text.Json;

namespace AuthSubsystem.Handlers.Keycloak;

public record KeycloakTokenClaims(string? Subject, string? SessionId, DateTimeOffset? ExpiresAt);

// Reads claims from a Keycloak JWT WITHOUT validating its signature.
// Only use on tokens Keycloak has already accepted (e.g. after a successful logout call).
internal static class KeycloakTokenReader
{
    public static KeycloakTokenClaims? TryReadClaims(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

            return new KeycloakTokenClaims(
                json.TryGetProperty("sub", out var sub) ? sub.GetString() : null,
                json.TryGetProperty("sid", out var sid) ? sid.GetString() : null,
                json.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var e) ? DateTimeOffset.FromUnixTimeSeconds(e) : null);
        }
        catch
        {
            return null;
        }
    }
}
