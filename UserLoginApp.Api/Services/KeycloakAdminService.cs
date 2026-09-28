using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace UserLoginApp.Api.Services;

public interface IKeycloakAdminService
{
    Task<string?> CreateUserAsync(string username, string email, string firstName, string lastName, string password, string userType = "mybdjobs");
    Task UpdateUserAsync(string keycloakId, string email, string firstName, string lastName, string? userType = null);
    Task SetEnabledAsync(string keycloakId, bool enabled);
    Task DeleteUserAsync(string keycloakId);
    Task ResetPasswordAsync(string keycloakId, string newPassword);
    Task AddUserToGroupAsync(string keycloakUserId, string groupName);
    Task RemoveUserFromGroupAsync(string keycloakUserId, string groupName);
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

        // 1. Try AdminRealm ("master") first
        var tokenUrlMaster = $"{BaseUrl}/realms/{AdminRealm}/protocol/openid-connect/token";
        var formMaster = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = AdminUsername,
            ["password"] = AdminPassword
        });

        var responseMaster = await client.PostAsync(tokenUrlMaster, formMaster);
        if (responseMaster.IsSuccessStatusCode)
        {
            var json = await responseMaster.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("access_token").GetString()!;
        }

        var masterErr = await responseMaster.Content.ReadAsStringAsync();

        // 2. Fallback: Try target Realm if bdjadmin is created inside target Realm
        if (!string.Equals(AdminRealm, Realm, StringComparison.OrdinalIgnoreCase))
        {
            var tokenUrlRealm = $"{BaseUrl}/realms/{Realm}/protocol/openid-connect/token";
            var formRealm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = _config["Keycloak:ClientId"] ?? "admin-cli",
                ["username"] = AdminUsername,
                ["password"] = AdminPassword
            });

            var responseRealm = await client.PostAsync(tokenUrlRealm, formRealm);
            if (responseRealm.IsSuccessStatusCode)
            {
                var jsonRealm = await responseRealm.Content.ReadFromJsonAsync<JsonElement>();
                return jsonRealm.GetProperty("access_token").GetString()!;
            }

            // Also try with admin-cli on target realm
            var formRealmAdminCli = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = "admin-cli",
                ["username"] = AdminUsername,
                ["password"] = AdminPassword
            });
            var responseRealmAdminCli = await client.PostAsync(tokenUrlRealm, formRealmAdminCli);
            if (responseRealmAdminCli.IsSuccessStatusCode)
            {
                var jsonRealmAdminCli = await responseRealmAdminCli.Content.ReadFromJsonAsync<JsonElement>();
                return jsonRealmAdminCli.GetProperty("access_token").GetString()!;
            }
        }

        throw new InvalidOperationException(
            $"Failed to obtain Keycloak Admin Token for user '{AdminUsername}' from '{BaseUrl}'. " +
            $"Master Realm Token URL ({tokenUrlMaster}) returned: {masterErr}. " +
            $"Please verify: 1. Is Keycloak BaseUrl '{BaseUrl}' correct or should it be 'https://auth.bdjobs.com'? " +
            $"2. Is user '{AdminUsername}' created in 'master' realm or '{Realm}' realm? " +
            $"3. Is the password '{AdminPassword}' correct for '{AdminUsername}'?");
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
            
            // 1. Check if group already exists
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

            // 2. If group does not exist, create it
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

                // Re-query to fetch ID
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

    public async Task RemoveUserFromGroupAsync(string keycloakUserId, string groupName)
    {
        try
        {
            var groupId = await EnsureGroupExistsAndGetIdAsync(groupName);
            if (string.IsNullOrEmpty(groupId)) return;

            var client = await AuthorizedClientAsync();
            var response = await client.DeleteAsync($"{BaseUrl}/admin/realms/{Realm}/users/{keycloakUserId}/groups/{groupId}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to remove user {UserId} from group {GroupName} (Status: {Status})", keycloakUserId, groupName, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user {UserId} from group {GroupName} in Keycloak", keycloakUserId, groupName);
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
                throw new InvalidOperationException($"Keycloak API call to '{userEndpointUrl}' failed with HTTP {response.StatusCode} ({(int)response.StatusCode}). Please verify that Realm '{Realm}' exists in Keycloak (casing matters) and Keycloak is running at '{BaseUrl}'. Details: {errDetails}");
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

    public async Task UpdateUserAsync(string keycloakId, string email, string firstName, string lastName, string? userType = null)
    {
        try
        {
            var client = await AuthorizedClientAsync();

            var bodyDict = new Dictionary<string, object>
            {
                ["email"] = email,
                ["firstName"] = firstName,
                ["lastName"] = lastName
            };

            if (!string.IsNullOrWhiteSpace(userType))
            {
                var normalizedUserType = userType.Trim().ToLower() switch
                {
                    "corporate" => "corporate",
                    "mis" => "mis",
                    _ => "mybdjobs"
                };

                bodyDict["attributes"] = new Dictionary<string, string[]>
                {
                    ["userType"] = new[] { normalizedUserType }
                };

                // Manage group memberships if userType changed
                var newGroupName = GetGroupNameForUserType(normalizedUserType);
                var knownGroups = new[] { "Corporate-Users", "MyBdjobs-Users", "MIS-Users" };

                foreach (var oldGroup in knownGroups)
                {
                    if (!string.Equals(oldGroup, newGroupName, StringComparison.OrdinalIgnoreCase))
                    {
                        await RemoveUserFromGroupAsync(keycloakId, oldGroup);
                    }
                }
                await AddUserToGroupAsync(keycloakId, newGroupName);
            }

            var content = new StringContent(JsonSerializer.Serialize(bodyDict), Encoding.UTF8, "application/json");
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
