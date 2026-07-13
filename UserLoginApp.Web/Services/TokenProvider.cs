namespace UserLoginApp.Web.Services;

/// <summary>
/// Scoped service that captures the Keycloak access token during the initial
/// HTTP request (_Host.cshtml) and makes it available throughout the Blazor circuit.
/// </summary>
public class TokenProvider
{
    public string? AccessToken { get; set; }
}
