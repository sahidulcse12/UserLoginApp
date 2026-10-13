namespace AuthSubsystem.Handlers.Sessions;

// Keycloak session ids (the "sid" claim) that were logged out, so their
// still-unexpired access tokens can be rejected immediately.
public interface IRevokedSessionStore
{
    Task RevokeAsync(string sessionId, DateTimeOffset until, CancellationToken cancellationToken = default);
    Task<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken = default);
}
