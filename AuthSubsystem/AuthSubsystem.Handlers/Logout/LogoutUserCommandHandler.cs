using Microsoft.Extensions.Logging;
using AuthSubsystem.Aggregators.Entities;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstructions;
using SharedSubsystem.Handlers.Events;
using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Handlers.Sessions;
using AuthSubsystem.Repositories.Abstractions;
using HandlerEventId = SharedSubsystem.Handlers.Events.EventId;

namespace AuthSubsystem.Handlers.Logout;

public class LogoutUserCommandHandler : ICommandHandler<LogoutUserCommand>
{
    private static readonly TimeSpan MinRevocationWindow = TimeSpan.FromMinutes(15);

    private readonly IAuthRepository _authRepo;
    private readonly IKeycloakAdminService _keycloak;
    private readonly IRevokedSessionStore _revokedSessions;
    private readonly ILogger<LogoutUserCommandHandler> _logger;

    public LogoutUserCommandHandler(
        IAuthRepository authRepo,
        IKeycloakAdminService keycloak,
        IRevokedSessionStore revokedSessions,
        ILogger<LogoutUserCommandHandler> logger)
    {
        _authRepo = authRepo;
        _keycloak = keycloak;
        _revokedSessions = revokedSessions;
        _logger = logger;
    }

    public async Task<IEnumerable<Event>> HandleAsync(LogoutUserCommand command)
    {
        var request = command.Request;

        // Validate
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return EventFactory.CreateFailureEvent(HandlerEventId.UserInputError, "RefreshToken is required.");

        // End Keycloak session
        var loggedOut = await _keycloak.LogoutAsync(request.RefreshToken);
        if (!loggedOut)
            return EventFactory.CreateFailureEvent(HandlerEventId.LogoutFailed, "Invalid or expired refresh token.");

        var claims = KeycloakTokenReader.TryReadClaims(request.RefreshToken);

        if (!string.IsNullOrEmpty(claims?.SessionId))
        {
            var minUntil = DateTimeOffset.UtcNow.Add(MinRevocationWindow);
            var until = claims.ExpiresAt > minUntil ? claims.ExpiresAt.Value : minUntil;
            await _revokedSessions.RevokeAsync(claims.SessionId, until);
        }
        else
        {
            _logger.LogWarning("Logout token has no 'sid' claim; access tokens stay valid until they expire");
        }

        var user = await FindLocalUserAsync(claims?.Subject, request.RefreshToken);
        if (user is null)
        {
            _logger.LogInformation("Keycloak session ended, but no local user matches this token");
            return EventFactory.CreateSuccessEvent(HandlerEventId.UserLoggedOut, new LogoutResponse());
        }

        if (user.RefreshToken is not null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _authRepo.UpdateUserAsync(user);
        }

        return EventFactory.CreateSuccessEvent(HandlerEventId.UserLoggedOut, new LogoutResponse());
    }

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
