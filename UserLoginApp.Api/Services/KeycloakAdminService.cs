using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace UserLoginApp.Api.Services;

public interface IKeycloakAdminService
{
    Task<string?> CreateUserAsync(string username, string email, string firstName, string lastName, string password, string userType = "mybdjobs");
    Task AddUserToGroupAsync(string keycloakUserId, string groupName);
    Task<string?> EnsureGroupExistsAndGetIdAsync(string groupName);
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
    private string ClientId => _config["Keycloak:ClientId"] ?? "user-login-keycloak";
    private string ClientSecret => _config["Keycloak:ClientSecret"] ?? "";

    public static string GetGroupNameForUserType(string userType)
    {
        return (userType ?? "").Trim().ToLower() switch
        {
            "corporate" => "Corporate-Users",
            "mis" => "MIS-Users",
            _ => "MyBdjobs-Users"
        };
    }

    private async Task<string> GetAdminTokenAsync()
    {
        using var client = new HttpClient();
        var errors = new List<string>();

        // 1. Primary Method: Client Credentials Flow (Service Account) on Target Realm
        if (!string.IsNullOrEmpty(ClientSecret))
        {
            var tokenUrlClientCreds = $"{BaseUrl}/realms/{Realm}/protocol/openid-connect/token";
            var formClientCreds = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            });

            try
            {
                var responseClientCreds = await client.PostAsync(tokenUrlClientCreds, formClientCreds);
                if (responseClientCreds.IsSuccessStatusCode)
                {
                    var json = await responseClientCreds.Content.ReadFromJsonAsync<JsonElement>();
                    if (json.TryGetProperty("access_token", out var tokenProp))
                    {
                        return tokenProp.GetString()!;
                    }
                }
                var err = await responseClientCreds.Content.ReadAsStringAsync();
                errors.Add($"Client Credentials ({tokenUrlClientCreds}): HTTP {(int)responseClientCreds.StatusCode} - {err}");
            }
            catch (Exception ex)
            {
                errors.Add($"Client Credentials exception: {ex.Message}");
            }
        }

        // 2. Secondary Method: Master Realm Password Grant (admin-cli)
        var tokenUrlMaster = $"{BaseUrl}/realms/{AdminRealm}/protocol/openid-connect/token";
        var formMaster = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = AdminUsername,
            ["password"] = AdminPassword
        });

        try
        {
            var responseMaster = await client.PostAsync(tokenUrlMaster, formMaster);
            if (responseMaster.IsSuccessStatusCode)
            {
                var json = await responseMaster.Content.ReadFromJsonAsync<JsonElement>();
                return json.GetProperty("access_token").GetString()!;
            }
            var masterErr = await responseMaster.Content.ReadAsStringAsync();
            errors.Add($"Master Realm Password Grant ({tokenUrlMaster}): HTTP {(int)responseMaster.StatusCode} - {masterErr}");
        }
        catch (Exception ex)
        {
            errors.Add($"Master Realm exception: {ex.Message}");
        }

        // 3. Fallback: Target Realm Password Grant with Client Credentials
        if (!string.Equals(AdminRealm, Realm, StringComparison.OrdinalIgnoreCase))
        {
            var tokenUrlRealm = $"{BaseUrl}/realms/{Realm}/protocol/openid-connect/token";
            var formRealmDict = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = ClientId,
                ["username"] = AdminUsername,
                ["password"] = AdminPassword
            };
            if (!string.IsNullOrEmpty(ClientSecret))
            {
                formRealmDict["client_secret"] = ClientSecret;
            }

            try
            {
                var responseRealm = await client.PostAsync(tokenUrlRealm, new FormUrlEncodedContent(formRealmDict));
                if (responseRealm.IsSuccessStatusCode)
                {
                    var jsonRealm = await responseRealm.Content.ReadFromJsonAsync<JsonElement>();
                    return jsonRealm.GetProperty("access_token").GetString()!;
                }
                var realmErr = await responseRealm.Content.ReadAsStringAsync();
                errors.Add($"Target Realm Password Grant ({tokenUrlRealm}): HTTP {(int)responseRealm.StatusCode} - {realmErr}");
            }
            catch (Exception ex)
            {
                errors.Add($"Target Realm exception: {ex.Message}");
            }
        }

        throw new InvalidOperationException(
            $"Failed to obtain Keycloak Admin Token for client '{ClientId}' / user '{AdminUsername}' from '{BaseUrl}'. " +
            $"Details: {string.Join(" | ", errors)}");
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

    public async Task<string?> EnsureGroupExistsAndGetIdAsync(string groupName)
    {
        try
        {
            var client = await AuthorizedClientAsync();
            
            var response = await client.GetAsync($"{BaseUrl}/admin/realms/{Realm}/groups?search={Uri.EscapeDataString(groupName)}");
            if (response.IsSuccessStatusCode)
            {
                var groups = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (groups.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in groups.EnumerateArray())
                    {
                        if (string.Equals(g.GetProperty("name").GetString(), groupName, StringComparison.OrdinalIgnoreCase))
                        {
                            return g.GetProperty("id").GetString();
                        }
                    }
                }
            }

            var groupBody = new { name = groupName };
            var content = new StringContent(JsonSerializer.Serialize(groupBody), Encoding.UTF8, "application/json");
            var createResponse = await client.PostAsync($"{BaseUrl}/admin/realms/{Realm}/groups", content);
            
            if (createResponse.IsSuccessStatusCode || createResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var location = createResponse.Headers.Location?.ToString();
                if (!string.IsNullOrEmpty(location))
                {
                    return location.Split('/').Last();
                }

                var retryResponse = await client.GetAsync($"{BaseUrl}/admin/realms/{Realm}/groups?search={Uri.EscapeDataString(groupName)}");
                if (retryResponse.IsSuccessStatusCode)
                {
                    var groups = await retryResponse.Content.ReadFromJsonAsync<JsonElement>();
                    if (groups.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var g in groups.EnumerateArray())
                        {
                            if (string.Equals(g.GetProperty("name").GetString(), groupName, StringComparison.OrdinalIgnoreCase))
                            {
                                return g.GetProperty("id").GetString();
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure group {GroupName} exists in Keycloak", groupName);
        }
        return null;
    }

    public async Task AddUserToGroupAsync(string keycloakUserId, string groupName)
    {
        try
        {
            var groupId = await EnsureGroupExistsAndGetIdAsync(groupName);
            if (string.IsNullOrEmpty(groupId))
            {
                _logger.LogWarning("Cannot add user {UserId} to group {GroupName} because group ID could not be resolved.", keycloakUserId, groupName);
                return;
            }

            var client = await AuthorizedClientAsync();
            var response = await client.PutAsync($"{BaseUrl}/admin/realms/{Realm}/users/{keycloakUserId}/groups/{groupId}", null);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to add user {UserId} to group {GroupName} (Status: {Status})", keycloakUserId, groupName, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user {UserId} to group {GroupName} in Keycloak", keycloakUserId, groupName);
        }
    }

    public async Task<string?> CreateUserAsync(string username, string email, string firstName, string lastName, string password, string userType = "mybdjobs")
    {
        try
        {
            var normalizedUserType = (userType ?? "").Trim().ToLower() switch
            {
                "corporate" => "corporate",
                "mis" => "mis",
                _ => "mybdjobs"
            };

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
                    ["userType"] = new[] { normalizedUserType }
                },
                credentials = new[]
                {
                    new { type = "password", value = password, temporary = false }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(userBody), Encoding.UTF8, "application/json");
            var userEndpointUrl = $"{BaseUrl}/admin/realms/{Realm}/users";
            var response = await client.PostAsync(userEndpointUrl, content);

            string? keycloakId = null;
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogWarning("User {Username} already exists in Keycloak. Fetching existing Keycloak ID.", username);
                keycloakId = await GetUserIdByUsernameAsync(username);
            }
            else if (!response.IsSuccessStatusCode)
            {
                var errDetails = await response.Content.ReadAsStringAsync();
                _logger.LogError("Keycloak User Creation Failed. URL: {Url}, Status: {Status}, Details: {Details}", userEndpointUrl, response.StatusCode, errDetails);
                throw new InvalidOperationException($"Keycloak API call to '{userEndpointUrl}' failed with HTTP {response.StatusCode}. Details: {errDetails}");
            }
            else
            {
                var location = response.Headers.Location?.ToString();
                keycloakId = location?.Split('/').Last();
            }

            if (!string.IsNullOrEmpty(keycloakId))
            {
                var groupName = GetGroupNameForUserType(normalizedUserType);
                await AddUserToGroupAsync(keycloakId, groupName);
            }

            return keycloakId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user {Username} in Keycloak", username);
            throw new InvalidOperationException($"Keycloak sync failed: {ex.Message}", ex);
        }
    }
}
