using System.Net.Mail;
using Microsoft.Extensions.Logging;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstractions;
using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Repositories.Abstractions;

namespace AuthSubsystem.Handlers.ForgotPassword;

public class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, AuthResult<ForgotPasswordResponse>>
{
    private readonly IAuthRepository _authRepo;
    private readonly IKeycloakAdminService _keycloak;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IAuthRepository authRepo,
        IKeycloakAdminService keycloak,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _authRepo = authRepo;
        _keycloak = keycloak;
        _logger = logger;
    }

    public async Task<AuthResult<ForgotPasswordResponse>> HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken = default)
    {
        var email = command.Request.Email?.Trim();

        // Validate Aggregates & Apply Business Rules
        if (string.IsNullOrWhiteSpace(email))
            return AuthResult<ForgotPasswordResponse>.Fail(400, "Email is required.");
        if (!MailAddress.TryCreate(email, out _))
            return AuthResult<ForgotPasswordResponse>.Fail(400, "Email is not a valid email address.");

        // From here on the response is the same whatever happens, so callers can't tell which emails are registered
        var user = await _authRepo.GetUserByUsernameOrEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            _logger.LogInformation("Forgot-password requested for an unknown or inactive account");
            return AuthResult<ForgotPasswordResponse>.Ok(new ForgotPasswordResponse());
        }

        // Users who haven't logged in since the Keycloak migration may not have a stored Keycloak id yet
        var keycloakId = !string.IsNullOrEmpty(user.KeycloakId)
            ? user.KeycloakId
            : await _keycloak.GetUserIdByUsernameAsync(user.Username);

        if (string.IsNullOrEmpty(keycloakId))
        {
            _logger.LogWarning("Forgot-password: user '{Username}' has no Keycloak account", user.Username);
            return AuthResult<ForgotPasswordResponse>.Ok(new ForgotPasswordResponse());
        }

        // Keycloak emails the user a link to its own reset-password page
        if (!await _keycloak.SendResetPasswordEmailAsync(keycloakId))
            _logger.LogError("Forgot-password: Keycloak could not send the reset email to user '{Username}'", user.Username);

        return AuthResult<ForgotPasswordResponse>.Ok(new ForgotPasswordResponse());
    }
}
