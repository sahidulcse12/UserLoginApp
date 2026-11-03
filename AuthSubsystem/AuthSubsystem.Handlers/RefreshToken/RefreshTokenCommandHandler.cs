using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstructions;
using SharedSubsystem.Handlers.Events;
using AuthSubsystem.Handlers.Keycloak;

namespace AuthSubsystem.Handlers.RefreshToken;

public class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand>
{
    private readonly IKeycloakAdminService _keycloak;

    public RefreshTokenCommandHandler(IKeycloakAdminService keycloak)
    {
        _keycloak = keycloak;
    }

    public async Task<IEnumerable<Event>> HandleAsync(RefreshTokenCommand command)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return EventFactory.CreateFailureEvent(EventId.UserInputError, "RefreshToken is required.");

        var tokenResponse = await _keycloak.RefreshTokenAsync(request.RefreshToken);
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            return EventFactory.CreateFailureEvent(EventId.UserAuthorizationFailed, "Invalid or expired refresh token.");

        return EventFactory.CreateSuccessEvent(EventId.UserAuthenticated, tokenResponse);
    }
}
