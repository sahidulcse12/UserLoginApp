using Microsoft.Extensions.Logging;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstractions;
using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Repositories.Abstractions;
using SharedSubsystem.DTOs.IntegrationEvents;

namespace AuthSubsystem.Handlers.Logout;

public class LogoutUserCommandHandler : ICommandHandler<LogoutUserCommand, AuthResult<LogoutResponse>>
{
    private readonly IAuthRepository _authRepo;
    private readonly IKeycloakAdminService _keycloak;
    private readonly IServiceBus _serviceBus;
    private readonly ILogger<LogoutUserCommandHandler> _logger;

    public LogoutUserCommandHandler(
        IAuthRepository authRepo,
        IKeycloakAdminService keycloak,
        IServiceBus serviceBus,
        ILogger<LogoutUserCommandHandler> logger)
    {
        _authRepo = authRepo;
        _keycloak = keycloak;
        _serviceBus = serviceBus;
        _logger = logger;
    }

    public async Task<AuthResult<LogoutResponse>> HandleAsync(LogoutUserCommand command, CancellationToken cancellationToken = default)
    {
        var request = command.Request;

        // 1. Validate Aggregates & Apply Business Rules
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return AuthResult<LogoutResponse>.Fail(400, "RefreshToken is required.");

        // 2. End the Keycloak session
        var loggedOut = await _keycloak.LogoutAsync(request.RefreshToken);
        if (!loggedOut)
            return AuthResult<LogoutResponse>.Fail(401, "Invalid or expired refresh token.");

        // 3. Clear the stored refresh token in Local Database
        var user = await _authRepo.GetUserByRefreshTokenAsync(request.RefreshToken);
        if (user is not null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _authRepo.UpdateUserAsync(user);

            // 4. Broadcast Event to ServiceBus
            await _serviceBus.BroadcastEventAsync(new UserLoggedOutEvent(
                user.Username,
                DateTime.UtcNow
            ), cancellationToken);
        }
        else
        {
            _logger.LogInformation("Keycloak session ended, but no local user holds this refresh token");
        }

        return AuthResult<LogoutResponse>.Ok(new LogoutResponse());
    }
}
