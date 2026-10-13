using SharedSubsystem.DTOs.Abstractions;

namespace AuthSubsystem.DTOs.Commands;

public record RegisterUserCommand(RegisterRequest Request) : ICommand<AuthResult<RegisterResponse>>;

public record LoginUserCommand(LoginRequest Request) : ICommand<AuthResult<TokenResponse>>;

public record RefreshTokenCommand(RefreshTokenRequest Request) : ICommand<AuthResult<TokenResponse>>;

public record LogoutUserCommand(LogoutRequest Request) : ICommand<AuthResult<LogoutResponse>>;
