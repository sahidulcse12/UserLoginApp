using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace UserLoginApp.Api.Services;

public interface IKeycloakAdminService
{
    Task<string?> CreateUserAsync(string username, string email, string firstName, string lastName, string password, string userType = "mybdjobs");
    Task UpdateUserAsync(string keycloakId, string email, string firstName, string lastName);
    Task SetEnabledAsync(string keycloakId, bool enabled);
    Task DeleteUserAsync(string keycloakId);
    Task ResetPasswordAsync(string keycloakId, string newPassword);
}

public class KeycloakAdminService : IKeycloakAdminService
{
    private readonly IConfiguration _config;
    private readonly ILogger<KeycloakAdminService> _logger;

    public KeycloakAdminService(IConfiguration config, ILogger<KeycloakAdminService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private string BaseUrl => _config["Keycloak:BaseUrl"]!;
    private string Realm => _config["Keycloak:Realm"]!;
    private string AdminRealm => _config["Keycloak:AdminRealm"] ?? "master";
    private string AdminUsername => _config["Keycloak:AdminUsername"]!;
    private string AdminPassword => _config["Keycloak:AdminPassword"]!;

    private async Task<string> GetAdminTokenAsync()
    {
        using var client = new HttpClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = AdminUsername,
            ["password"] = AdminPassword
        });

        var response = await client.PostAsync($"{BaseUrl}/realms/{AdminRealm}/protocol/openid-connect/token", form);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }

    private async Task<HttpClient> AuthorizedClientAsync()
    {
        var token = await GetAdminTokenAsync();
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<string?> GetUserIdByUsernameAsync(string username)
    {
        try
        {
            var client = await AuthorizedClientAsync();
            var response = await client.GetAsync($"{BaseUrl}/admin/realms/{Realm}/users?username={Uri.EscapeDataString(username)}&exact=true");
            if (response.IsSuccessStatusCode)
            {
                var users = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (users.ValueKind == JsonValueKind.Array && users.GetArrayLength() > 0)
                {
                    return users[0].GetProperty("id").GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch user ID for {Username} from Keycloak", username);
        }
        return null;
    }

    public async Task<string?> CreateUserAsync(string username, string email, string firstName, string lastName, string password, string userType = "mybdjobs")
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
                attributes = new Dictionary<string, string[]>
                {
                    ["userType"] = new[] { string.IsNullOrWhiteSpace(userType) ? "mybdjobs" : userType }
                },
                credentials = new[]
                {
                    new { type = "password", value = password, temporary = false }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(userBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{BaseUrl}/admin/realms/{Realm}/users", content);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogWarning("User {Username} already exists in Keycloak. Fetching existing Keycloak ID.", username);
                return await GetUserIdByUsernameAsync(username);
            }

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
