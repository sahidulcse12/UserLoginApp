using Microsoft.Extensions.Logging;
using AuthSubsystem.Aggregators.Entities;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstractions;
using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Repositories.Abstractions;
using SharedSubsystem.DTOs.IntegrationEvents;

namespace AuthSubsystem.Handlers.Login;

public class LoginUserCommandHandler : ICommandHandler<LoginUserCommand, AuthResult<TokenResponse>>
{
    private readonly IAuthRepository _authRepo;
    private readonly IKeycloakAdminService _keycloak;
    private readonly IServiceBus _serviceBus;
    private readonly ILogger<LoginUserCommandHandler> _logger;

    public LoginUserCommandHandler(
        IAuthRepository authRepo,
        IKeycloakAdminService keycloak,
        IServiceBus serviceBus,
        ILogger<LoginUserCommandHandler> logger)
    {
        _authRepo = authRepo;
        _keycloak = keycloak;
        _serviceBus = serviceBus;
        _logger = logger;
    }

    public async Task<AuthResult<TokenResponse>> HandleAsync(LoginUserCommand command, CancellationToken cancellationToken = default)
    {
        var request = command.Request;

        // 1. Validate Aggregates & Apply Business Rules
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return AuthResult<TokenResponse>.Fail(400, "Username and Password are required.");

        // 2. Authenticate with Keycloak
        TokenResponse? tokenResponse = null;
        string? keycloakLoginError = null;

        try
        {
            tokenResponse = await _keycloak.LoginUserAsync(request.Username, request.Password);
        }
        catch (InvalidOperationException ex)
        {
            keycloakLoginError = ex.Message;
            _logger.LogWarning("Keycloak direct login failed for '{Username}': {Reason}", request.Username, keycloakLoginError);
        }

        // 3. Fallback to Legacy Migration if needed
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            var migrationResult = await HandleLegacyAdminMigrationAsync(request.Username, request.Password);
            if (migrationResult is not null)
            {
                if (!migrationResult.Success)
                    return migrationResult;

                tokenResponse = migrationResult.Data;
            }
        }

        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            var reason = keycloakLoginError ?? "Invalid credentials or Keycloak authentication failed.";
            return AuthResult<TokenResponse>.Fail(401, reason);
        }

        // 4. Update Token in Local Database
        await SyncLocalUserTokenAsync(request.Username, tokenResponse);

        // 5. Broadcast Event to ServiceBus
        await _serviceBus.BroadcastEventAsync(new UserLoggedInEvent(
            request.Username,
            DateTime.UtcNow
        ), cancellationToken);

        return AuthResult<TokenResponse>.Ok(tokenResponse);
    }

    private async Task<AuthResult<TokenResponse>?> HandleLegacyAdminMigrationAsync(string usernameInput, string passwordInput)
    {
        var legacyAdmin = await _authRepo.GetLegacyAdminUserAsync(usernameInput, passwordInput);
        if (legacyAdmin is null) return null;

        _logger.LogInformation("Legacy admin '{Username}' found in dbo.ADMIN. Migrating to Keycloak...", legacyAdmin.Username);

        var username  = legacyAdmin.Username;
        var email     = legacyAdmin.Email;
        var userType  = "mis";
        var firstName = username;
        var lastName  = "-";

        string? keycloakId;
        try
        {
            keycloakId = await _keycloak.CreateUserAsync(
                username, email, firstName, lastName, passwordInput, userType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision legacy admin user '{Username}' in Keycloak", username);
            return AuthResult<TokenResponse>.Fail(502, $"Legacy user migration to Keycloak failed: {ex.Message}");
        }

        var existingUser = await _authRepo.GetUserByUsernameOrEmailExactAsync(username, email);
        if (existingUser != null)
        {
            existingUser.KeycloakId ??= keycloakId;
            existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordInput);
            existingUser.UpdatedAt    = DateTime.UtcNow;
            await _authRepo.UpdateUserAsync(existingUser);
        }
        else
        {
            var newUser = new User
            {
                Username     = username,
                Email        = email,
                FirstName    = firstName,
                LastName     = lastName,
                UserType     = userType,
                KeycloakId   = keycloakId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(passwordInput),
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            };
            await _authRepo.AddUserAsync(newUser);
        }

        return await LoginMigratedUserWithRetryAsync(username, passwordInput, keycloakId);
    }

    private async Task<AuthResult<TokenResponse>> LoginMigratedUserWithRetryAsync(
        string username, string password, string? keycloakId)
    {
        try
        {
            var token = await _keycloak.LoginUserAsync(username, password);
            return AuthResult<TokenResponse>.Ok(token!);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not fully set up", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Keycloak account for '{Username}' has required actions. Clearing and retrying...", username);

            var kcId = keycloakId ?? await _keycloak.GetUserIdByUsernameAsync(username);
            if (!string.IsNullOrEmpty(kcId))
                await _keycloak.ClearRequiredActionsAsync(kcId);

            try
            {
                var retriedToken = await _keycloak.LoginUserAsync(username, password);
                return AuthResult<TokenResponse>.Ok(retriedToken!);
            }
            catch (InvalidOperationException retryEx)
            {
                _logger.LogError(retryEx, "Login still failed after clearing required actions for '{Username}'", username);
                return AuthResult<TokenResponse>.Fail(502, $"User migrated but Keycloak login failed: {retryEx.Message}");
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Keycloak login failed for migrated user '{Username}'", username);
            return AuthResult<TokenResponse>.Fail(502, $"User migrated but Keycloak login failed: {ex.Message}");
        }
    }

    private async Task SyncLocalUserTokenAsync(string username, TokenResponse tokenResponse)
    {
        var user = await _authRepo.GetUserByUsernameOrEmailAsync(username);
        if (user is null) return;

        user.RefreshToken = tokenResponse.RefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddSeconds(
            tokenResponse.RefreshExpiresIn > 0 ? tokenResponse.RefreshExpiresIn : 1800);
        user.UpdatedAt = DateTime.UtcNow;

        await _authRepo.UpdateUserAsync(user);

        tokenResponse.User = new UserDto
        {
            Id         = user.Id,
            Username   = user.Username,
            Email      = user.Email,
            FirstName  = user.FirstName,
            LastName   = user.LastName,
            UserType   = user.UserType,
            KeycloakId = user.KeycloakId
        };
    }
}
