using SharedSubsystem.DTOs.Abstractions;

namespace AuthSubsystem.DTOs.Commands;

public record RegisterUserCommand(RegisterRequest Request) : ICommand;

public record LoginUserCommand(LoginRequest Request) : ICommand;

public record RefreshTokenCommand(RefreshTokenRequest Request) : ICommand;

public record LogoutUserCommand(LogoutRequest Request) : ICommand;
