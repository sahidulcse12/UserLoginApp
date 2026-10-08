using Microsoft.AspNetCore.Mvc;
using UserLoginApp.Api.Services;
using UserLoginApp.Api.Models.DTOs;

namespace UserLoginApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return result.Success
            ? Ok(result.Data)
            : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return result.Success
            ? Ok(result.Data)
            : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request);
        return result.Success
            ? Ok(result.Data)
            : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }
}
