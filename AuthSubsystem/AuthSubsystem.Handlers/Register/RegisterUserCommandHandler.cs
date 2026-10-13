using Microsoft.Extensions.Logging;
using AuthSubsystem.Aggregators.Entities;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstractions;
using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Repositories.Abstractions;
using SharedSubsystem.DTOs.IntegrationEvents;

namespace AuthSubsystem.Handlers.Register;

public class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, AuthResult<RegisterResponse>>
{
    private readonly IAuthRepository _authRepo;
    private readonly IKeycloakAdminService _keycloak;
    private readonly IServiceBus _serviceBus;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IAuthRepository authRepo,
        IKeycloakAdminService keycloak,
        IServiceBus serviceBus,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _authRepo = authRepo;
        _keycloak = keycloak;
        _serviceBus = serviceBus;
        _logger = logger;
    }

    public async Task<AuthResult<RegisterResponse>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        var request = command.Request;

        // 1. Validate Aggregates & Apply Business Rules
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return AuthResult<RegisterResponse>.Fail(400, "Username and Password are required.");

        var normalizedType = (request.UserType ?? string.Empty).Trim().ToLower();
        if (normalizedType is not ("corporate" or "mybdjobs" or "mis"))
            return AuthResult<RegisterResponse>.Fail(400, "Invalid UserType. Allowed values are 'corporate', 'mybdjobs', or 'mis'.");

        var localExists = await _authRepo.UserExistsAsync(request.Username, request.Email);
        if (localExists)
            return AuthResult<RegisterResponse>.Fail(409, "User with this username or email already exists in local database.");

        // 2. Provision in Keycloak
        string? keycloakId;
        try
        {
            keycloakId = await _keycloak.CreateUserAsync(
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Password,
                normalizedType
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register user in Keycloak for '{Username}'", request.Username);
            return AuthResult<RegisterResponse>.Fail(500, $"Failed to register user in Keycloak: {ex.Message}");
        }

        // 3. Persist in MS SQL Server
        var user = new User
        {
            Username     = request.Username,
            Email        = request.Email,
            FirstName    = request.FirstName,
            LastName     = request.LastName,
            UserType     = normalizedType,
            KeycloakId   = keycloakId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow
        };

        await _authRepo.AddUserAsync(user);

        // 4. Broadcast Event to ServiceBus
        await _serviceBus.BroadcastEventAsync(new UserRegisteredEvent(
            user.Id,
            user.Username,
            user.Email,
            user.UserType,
            user.KeycloakId,
            DateTime.UtcNow
        ), cancellationToken);

        // 5. Return Result
        return AuthResult<RegisterResponse>.Ok(new RegisterResponse
        {
            Id         = user.Id,
            KeycloakId = user.KeycloakId,
            Username   = user.Username,
            Email      = user.Email,
            FirstName  = user.FirstName,
            LastName   = user.LastName,
            UserType   = user.UserType,
            Message    = "User registered in Keycloak and synced to local database successfully."
        });
    }
}
