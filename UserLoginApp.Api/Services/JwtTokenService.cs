using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using UserLoginApp.Api.Models.Entities;

namespace UserLoginApp.Api.Services;

public interface IJwtTokenService
{
    (string AccessToken, DateTime ExpiresAt) GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> features);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public (string AccessToken, DateTime ExpiresAt) GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> features)
    {
        var jwtSecret = _config["Jwt:Secret"] ?? "SuperSecretKeyForUserLoginAppLocalAuthenticationSystem2026!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expirationMinutes = double.TryParse(_config["Jwt:AccessTokenExpirationMinutes"], out var mins) ? mins : 60;
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sub", user.Id.ToString()),
            new("preferred_username", user.Username),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("userType", user.UserType)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        foreach (var feature in features)
        {
            claims.Add(new Claim("feature", feature));
        }

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "UserLoginAppLocalIssuer",
            audience: _config["Jwt:Audience"] ?? "UserLoginAppLocalAudience",
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var jwtSecret = _config["Jwt:Secret"] ?? "SuperSecretKeyForUserLoginAppLocalAuthenticationSystem2026!";
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = _config["Jwt:Audience"] ?? "UserLoginAppLocalAudience",
            ValidateIssuer = true,
            ValidIssuer = _config["Jwt:Issuer"] ?? "UserLoginAppLocalIssuer",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = false // Don't validate lifetime here as we're validating an expired token
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }
            return principal;
        }
        catch
        {
            return null;
        }
    }
}
