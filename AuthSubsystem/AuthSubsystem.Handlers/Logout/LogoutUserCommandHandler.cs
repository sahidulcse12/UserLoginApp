using Microsoft.Extensions.Logging;
using AuthSubsystem.Aggregators.Entities;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstractions;
using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Handlers.Sessions;
using AuthSubsystem.Repositories.Abstractions;
using SharedSubsystem.DTOs.IntegrationEvents;

namespace AuthSubsystem.Handlers.Logout;

public class LogoutUserCommandHandler : ICommandHandler<LogoutUserCommand, AuthResult<LogoutResponse>>
{
    // Access tokens outlive their session by at most their own lifespan (Keycloak default: 5 min),
    // so keep a revoked session blocked at least this long even if the refresh token has expired.
    private static readonly TimeSpan MinRevocationWindow = TimeSpan.FromMinutes(15);

    private readonly IAuthRepository _authRepo;
    private readonly IKeycloakAdminService _keycloak;
    private readonly IRevokedSessionStore _revokedSessions;
    private readonly IServiceBus _serviceBus;
    private readonly ILogger<LogoutUserCommandHandler> _logger;

    public LogoutUserCommandHandler(
        IAuthRepository authRepo,
        IKeycloakAdminService keycloak,
        IRevokedSessionStore revokedSessions,
        IServiceBus serviceBus,
        ILogger<LogoutUserCommandHandler> logger)
    {
        _authRepo = authRepo;
        _keycloak = keycloak;
        _revokedSessions = revokedSessions;
        _serviceBus = serviceBus;
        _logger = logger;
    }

    public async Task<AuthResult<LogoutResponse>> HandleAsync(LogoutUserCommand command, CancellationToken cancellationToken = default)
    {
        var request = command.Request;

        // 1. Validate Aggregates & Apply Business Rules
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return AuthResult<LogoutResponse>.Fail(400, "RefreshToken is required.");

        // 2. End the Keycloak session (Keycloak validates the refresh token here)
        var loggedOut = await _keycloak.LogoutAsync(request.RefreshToken);
        if (!loggedOut)
            return AuthResult<LogoutResponse>.Fail(401, "Invalid or expired refresh token.");

        // Safe to read without validation: Keycloak has just accepted this token
        var claims = KeycloakTokenReader.TryReadClaims(request.RefreshToken);

        // 3. Revoke the session so its unexpired access tokens are rejected by this API immediately
        if (!string.IsNullOrEmpty(claims?.SessionId))
        {
            var minUntil = DateTimeOffset.UtcNow.Add(MinRevocationWindow);
            var until = claims.ExpiresAt > minUntil ? claims.ExpiresAt.Value : minUntil;
            await _revokedSessions.RevokeAsync(claims.SessionId, until, cancellationToken);
        }
        else
        {
            _logger.LogWarning("Logout token has no 'sid' claim; access tokens stay valid until they expire");
        }

        // 4. Clear the stored refresh token in Local Database
        var user = await FindLocalUserAsync(claims?.Subject, request.RefreshToken);
        if (user is null)
        {
            _logger.LogInformation("Keycloak session ended, but no local user matches this token");
            return AuthResult<LogoutResponse>.Ok(new LogoutResponse());
        }

        // Only an active local session is reported; repeat logouts stay quiet
        if (user.RefreshToken is not null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _authRepo.UpdateUserAsync(user);

            // 5. Broadcast Event to ServiceBus
            await _serviceBus.BroadcastEventAsync(new UserLoggedOutEvent(
                user.Username,
                DateTime.UtcNow
            ), cancellationToken);
        }

        return AuthResult<LogoutResponse>.Ok(new LogoutResponse());
    }

    // Match by Keycloak user id first: after /refresh-token the stored refresh token is stale
    private async Task<User?> FindLocalUserAsync(string? keycloakId, string refreshToken)
    {
        if (!string.IsNullOrEmpty(keycloakId))
        {
            var user = await _authRepo.GetActiveUserByKeycloakIdOrUsernameAsync(keycloakId, null);
            if (user is not null) return user;
        }

        return await _authRepo.GetUserByRefreshTokenAsync(refreshToken);
    }
}
