namespace UserLoginApp.Api.Services.Keycloak;

public class KeycloakConfig
{
    public string BaseUrl { get; init; } = string.Empty;
    public string Realm { get; init; } = string.Empty;
    public string AdminRealm { get; init; } = "master";
    public string AdminUsername { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
    public string ClientId { get; init; } = "user-login-keycloak";
    public string ClientSecret { get; init; } = string.Empty;

    public static KeycloakConfig FromConfiguration(IConfiguration config) => new()
    {
        BaseUrl       = config["Keycloak:BaseUrl"] ?? throw new InvalidOperationException("Keycloak:BaseUrl is not configured."),
        Realm         = config["Keycloak:Realm"] ?? throw new InvalidOperationException("Keycloak:Realm is not configured."),
        AdminRealm    = config["Keycloak:AdminRealm"] ?? "master",
        AdminUsername = config["Keycloak:AdminUsername"] ?? throw new InvalidOperationException("Keycloak:AdminUsername is not configured."),
        AdminPassword = config["Keycloak:AdminPassword"] ?? throw new InvalidOperationException("Keycloak:AdminPassword is not configured."),
        ClientId      = config["Keycloak:ClientId"] ?? "user-login-keycloak",
        ClientSecret  = config["Keycloak:ClientSecret"] ?? string.Empty,
    };
}
