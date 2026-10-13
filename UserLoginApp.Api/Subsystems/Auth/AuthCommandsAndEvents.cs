using UserLoginApp.Api.Core.Messaging;
using UserLoginApp.Api.Models.DTOs;

namespace UserLoginApp.Api.Subsystems.Auth;

// ── Commands ─────────────────────────────────────────────────────────────

public record RegisterUserCommand(RegisterRequest Request) : ICommand<AuthResult<RegisterResponse>>;

public record LoginUserCommand(LoginRequest Request) : ICommand<AuthResult<TokenResponse>>;

public record RefreshTokenCommand(RefreshTokenRequest Request) : ICommand<AuthResult<TokenResponse>>;

// ── Events ───────────────────────────────────────────────────────────────

public record UserRegisteredEvent(
    Guid UserId,
    string Username,
    string Email,
    string UserType,
    string? KeycloakId,
    DateTime OccurredOn
) : IEvent;

public record UserLoggedInEvent(
    string Username,
    DateTime OccurredOn
) : IEvent;
