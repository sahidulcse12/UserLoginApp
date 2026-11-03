using Microsoft.Extensions.Logging;
using AuthSubsystem.Aggregators.Entities;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstructions;
using SharedSubsystem.Handlers.Events;
using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Repositories.Abstractions;
using HandlerEventId = SharedSubsystem.Handlers.Events.EventId;

namespace AuthSubsystem.Handlers.Register;

public class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand>
{
    private readonly IAuthRepository _authRepo;
    private readonly IKeycloakAdminService _keycloak;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IAuthRepository authRepo,
        IKeycloakAdminService keycloak,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _authRepo = authRepo;
        _keycloak = keycloak;
        _logger = logger;
    }

    public async Task<IEnumerable<Event>> HandleAsync(RegisterUserCommand command)
    {
        var request = command.Request;

        // 1. Validate Input
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return EventFactory.CreateFailureEvent(HandlerEventId.UserInputError, "Username and Password are required.");

        var normalizedType = (request.UserType ?? string.Empty).Trim().ToLower();
        if (normalizedType is not ("corporate" or "mybdjobs" or "mis"))
            return EventFactory.CreateFailureEvent(HandlerEventId.UserInputError, "Invalid UserType. Allowed values are 'corporate', 'mybdjobs', or 'mis'.");

        var localExists = await _authRepo.UserExistsAsync(request.Username, request.Email);
        if (localExists)
            return EventFactory.CreateFailureEvent(HandlerEventId.SecurityMismatch, "User with this username or email already exists in local database.");

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
            return EventFactory.CreateFailureEvent(HandlerEventId.InternalServerError, $"Failed to register user in Keycloak: {ex.Message}");
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

        // 4. Return Success Event
        var response = new RegisterResponse
        {
            Id         = user.Id,
            KeycloakId = user.KeycloakId,
            Username   = user.Username,
            Email      = user.Email,
            FirstName  = user.FirstName,
            LastName   = user.LastName,
            UserType   = user.UserType,
            Message    = "User registered in Keycloak and synced to local database successfully."
        };

        return EventFactory.CreateSuccessEvent(HandlerEventId.ResourceCreated, response);
    }
}
