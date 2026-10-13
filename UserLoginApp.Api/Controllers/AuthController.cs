using Microsoft.AspNetCore.Mvc;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Orchestrators;

namespace UserLoginApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthOrchestrator _authOrchestrator;

    public AuthController(IAuthOrchestrator authOrchestrator)
    {
        _authOrchestrator = authOrchestrator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authOrchestrator.RegisterAsync(request);
        return result.Success
            ? Ok(result.Data)
            : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authOrchestrator.LoginAsync(request);
        return result.Success
            ? Ok(result.Data)
            : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authOrchestrator.RefreshTokenAsync(request);
        return result.Success
            ? Ok(result.Data)
            : StatusCode(result.StatusCode, new { message = result.ErrorMessage });
    }
}
