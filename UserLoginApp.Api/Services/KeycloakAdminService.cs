using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace UserLoginApp.Api.Services;

public interface IKeycloakAdminService
{
    Task<string?> CreateUserAsync(string username, string email, string firstName, string lastName, string password);
    Task UpdateUserAsync(string keycloakId, string email, string firstName, string lastName);
    Task SetEnabledAsync(string keycloakId, bool enabled);
    Task DeleteUserAsync(string keycloakId);
    Task ResetPasswordAsync(string keycloakId, string newPassword);
}

public class KeycloakAdminService : IKeycloakAdminService
{
    private readonly IConfiguration _config;
    private readonly ILogger<KeycloakAdminService> _logger;
    private readonly HttpClient _http;

    private string BaseUrl => _config["Keycloak:BaseUrl"]!;
    private string Realm => _config["Keycloak:Realm"]!;
    private string AdminUsername => _config["Keycloak:AdminUsername"]!;
    private string AdminPassword => _config["Keycloak:AdminPassword"]!;

    public KeycloakAdminService(IConfiguration config, ILogger<KeycloakAdminService> logger)
    {
        _config = config;
        _logger = logger;
        _http = new HttpClient();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = AdminUsername,
            ["password"] = AdminPassword
        });

        var response = await _http.PostAsync($"{BaseUrl}/realms/{Realm}/protocol/openid-connect/token", form);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }

    private async Task<HttpClient> AuthorizedClientAsync()
    {
        var token = await GetAdminTokenAsync();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _http;
    }

    public async Task<string?> CreateUserAsync(string username, string email, string firstName, string lastName, string password)
    {
        try
        {
            var client = await AuthorizedClientAsync();

            var userBody = new
            {
                username,
                email,
                firstName,
                lastName,
                enabled = true,
                credentials = new[]
                {
                    new { type = "password", value = password, temporary = false }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(userBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{BaseUrl}/admin/realms/{Realm}/users", content);
            response.EnsureSuccessStatusCode();

            // Keycloak returns the new user's URL in the Location header
            var location = response.Headers.Location?.ToString();
            return location?.Split('/').Last();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user {Username} in Keycloak", username);
            throw new InvalidOperationException($"Keycloak sync failed: {ex.Message}", ex);
        }
    }

    public async Task UpdateUserAsync(string keycloakId, string email, string firstName, string lastName)
    {
        try
        {
            var client = await AuthorizedClientAsync();

            var body = new { email, firstName, lastName };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"{BaseUrl}/admin/realms/{Realm}/users/{keycloakId}", content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user {KeycloakId} in Keycloak", keycloakId);
            throw new InvalidOperationException($"Keycloak sync failed: {ex.Message}", ex);
        }
    }

    public async Task SetEnabledAsync(string keycloakId, bool enabled)
    {
        try
        {
            var client = await AuthorizedClientAsync();

            var body = new { enabled };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"{BaseUrl}/admin/realms/{Realm}/users/{keycloakId}", content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set enabled={Enabled} for user {KeycloakId} in Keycloak", enabled, keycloakId);
            throw new InvalidOperationException($"Keycloak sync failed: {ex.Message}", ex);
        }
    }

    public async Task DeleteUserAsync(string keycloakId)
    {
        try
        {
            var client = await AuthorizedClientAsync();
            var response = await client.DeleteAsync($"{BaseUrl}/admin/realms/{Realm}/users/{keycloakId}");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {KeycloakId} from Keycloak", keycloakId);
            throw new InvalidOperationException($"Keycloak sync failed: {ex.Message}", ex);
        }
    }

    public async Task ResetPasswordAsync(string keycloakId, string newPassword)
    {
        try
        {
            var client = await AuthorizedClientAsync();

            var body = new { type = "password", value = newPassword, temporary = false };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await client.PutAsync(
                $"{BaseUrl}/admin/realms/{Realm}/users/{keycloakId}/reset-password", content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset password for user {KeycloakId} in Keycloak", keycloakId);
            throw new InvalidOperationException($"Keycloak sync failed: {ex.Message}", ex);
        }
    }
}
