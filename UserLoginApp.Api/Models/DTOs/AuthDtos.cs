namespace UserLoginApp.Api.Models.DTOs;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public List<string> Features { get; set; } = new();
}
