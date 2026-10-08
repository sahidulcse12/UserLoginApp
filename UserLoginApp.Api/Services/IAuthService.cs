using UserLoginApp.Api.Models.DTOs;

namespace UserLoginApp.Api.Services;

public interface IAuthService
{
    Task<AuthResult<RegisterResponse>> RegisterAsync(RegisterRequest request);
    Task<AuthResult<TokenResponse>> LoginAsync(LoginRequest request);
    Task<AuthResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request);
}
