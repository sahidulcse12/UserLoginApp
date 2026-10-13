using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstractions;
using AuthSubsystem.Handlers.Keycloak;

namespace AuthSubsystem.Handlers.RefreshToken;

public class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AuthResult<TokenResponse>>
{
    private readonly IKeycloakAdminService _keycloak;

    public RefreshTokenCommandHandler(IKeycloakAdminService keycloak)
    {
        _keycloak = keycloak;
    }

    public async Task<AuthResult<TokenResponse>> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return AuthResult<TokenResponse>.Fail(400, "RefreshToken is required.");

        var tokenResponse = await _keycloak.RefreshTokenAsync(request.RefreshToken);
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            return AuthResult<TokenResponse>.Fail(401, "Invalid or expired refresh token.");

        return AuthResult<TokenResponse>.Ok(tokenResponse);
    }
}
