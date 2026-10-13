using Microsoft.AspNetCore.Mvc;
using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using SharedSubsystem.Handlers.Abstractions;

namespace AuthSubsystem.APIs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IServiceBus _serviceBus;

    public AuthController(IServiceBus serviceBus)
    {
        _serviceBus = serviceBus;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _serviceBus.SendCommandAsync(new RegisterUserCommand(request));
        return result.Success
            ? Ok(result.Data)
            : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _serviceBus.SendCommandAsync(new LoginUserCommand(request));
        return result.Success
            ? Ok(result.Data)
            : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _serviceBus.SendCommandAsync(new RefreshTokenCommand(request));
        return result.Success
            ? Ok(result.Data)
            : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var result = await _serviceBus.SendCommandAsync(new LogoutUserCommand(request));
        return result.Success
            ? Ok(result.Data)
            : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }
}
