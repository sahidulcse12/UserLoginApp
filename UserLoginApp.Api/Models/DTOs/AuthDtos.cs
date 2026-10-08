namespace UserLoginApp.Api.Models.DTOs;

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserType { get; set; } = "mybdjobs"; // "mis", "corporate", or "mybdjobs"
}

public class RegisterResponse
{
    public Guid Id { get; set; }
    public string? KeycloakId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string Message { get; set; } = "User registered in Keycloak and synced to local database successfully.";
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public int RefreshExpiresIn { get; set; }  // Refresh token own TTL (from Keycloak "refresh_expires_in")
    public string TokenType { get; set; } = "Bearer";
    public UserDto? User { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string? KeycloakId { get; set; }
}

public class LegacyAdminUserDto
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class AuthResult<T>
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public T? Data { get; set; }

    public static AuthResult<T> Ok(T data) => new() 
    { 
        Success = true, 
        StatusCode = 200, 
        Data = data 
    };

    public static AuthResult<T> Fail(int statusCode, string message) => new() 
    { 
        Success = false, 
        StatusCode = statusCode, 
        ErrorMessage = message 
    };
}
