using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using Microsoft.AspNetCore.Mvc;
using ServiceBus.Handlers;

namespace AuthSubsystem.APIs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IServiceBus serviceBus) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var events = await serviceBus.SendCommandAsync(new RegisterUserCommand(request));
        return Ok(events);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var events = await serviceBus.SendCommandAsync(new LoginUserCommand(request));
        return Ok(events);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var events = await serviceBus.SendCommandAsync(new RefreshTokenCommand(request));
        return Ok(events);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var events = await serviceBus.SendCommandAsync(new LogoutUserCommand(request));
        return Ok(events);
    }
}
