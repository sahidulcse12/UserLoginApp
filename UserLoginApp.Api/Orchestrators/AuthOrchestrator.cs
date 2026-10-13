using UserLoginApp.Api.Core.Messaging;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Subsystems.Auth;

namespace UserLoginApp.Api.Orchestrators;

public interface IAuthOrchestrator
{
    Task<AuthResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
}

public class AuthOrchestrator : IAuthOrchestrator
{
    private readonly IServiceBus _serviceBus;
    private readonly ILogger<AuthOrchestrator> _logger;

    public AuthOrchestrator(IServiceBus serviceBus, ILogger<AuthOrchestrator> logger)
    {
        _serviceBus = serviceBus;
        _logger = logger;
    }

    public async Task<AuthResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[AuthOrchestrator] Dispatching RegisterUserCommand for '{Username}'", request.Username);
        return await _serviceBus.SendCommandAsync(new RegisterUserCommand(request), cancellationToken);
    }

    public async Task<AuthResult<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[AuthOrchestrator] Dispatching LoginUserCommand for '{Username}'", request.Username);
        return await _serviceBus.SendCommandAsync(new LoginUserCommand(request), cancellationToken);
    }

    public async Task<AuthResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[AuthOrchestrator] Dispatching RefreshTokenCommand");
        return await _serviceBus.SendCommandAsync(new RefreshTokenCommand(request), cancellationToken);
    }
}
