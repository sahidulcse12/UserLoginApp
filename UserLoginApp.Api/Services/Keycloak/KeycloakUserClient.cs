using System.Text;
using System.Text.Json;

namespace UserLoginApp.Api.Services.Keycloak;

internal sealed class KeycloakUserClient
{
    private readonly KeycloakConfig _cfg;
    private readonly KeycloakTokenClient _tokenClient;
    private readonly ILogger _logger;

    public KeycloakUserClient(KeycloakConfig cfg, KeycloakTokenClient tokenClient, ILogger logger)
    {
        _cfg         = cfg;
        _tokenClient = tokenClient;
        _logger      = logger;
    }

    public async Task<string?> GetUserIdByUsernameAsync(string username)
    {
        try
        {
            var http     = await _tokenClient.CreateAuthorizedClientAsync();
            var url      = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users?username={Uri.EscapeDataString(username)}&exact=true";
            var response = await http.GetAsync(url);

            if (!response.IsSuccessStatusCode) return null;

            var users = await response.Content.ReadFromJsonAsync<JsonElement>();
            return users.ValueKind == JsonValueKind.Array && users.GetArrayLength() > 0
                ? users[0].GetProperty("id").GetString()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Keycloak user ID for '{Username}'", username);
            return null;
        }
    }

    public async Task<string?> CreateOrSyncUserAsync(
        string username, string email, string firstName, string lastName,
        string password, string normalizedUserType)
    {
        var http = await _tokenClient.CreateAuthorizedClientAsync();
        var url  = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users";

        var body    = BuildCreateUserBody(username, email, firstName, lastName, password, normalizedUserType);
        var content = JsonContent(body);
        var resp    = await http.PostAsync(url, content);

        string? keycloakId;

        if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("User '{Username}' already exists in Keycloak — syncing profile and password.", username);
            keycloakId = await GetUserIdByUsernameAsync(username);
            if (keycloakId is null)
            {
                _logger.LogError("409 Conflict for '{Username}' but keycloakId could not be resolved.", username);
                return null;
            }
            await UpdateUserProfileAsync(http, keycloakId, username, firstName, lastName, email, normalizedUserType);
            await SetUserPasswordAsync(http, keycloakId, password);
        }
        else if (!resp.IsSuccessStatusCode)
        {
            var details = await resp.Content.ReadAsStringAsync();
            _logger.LogError("Keycloak user creation failed — HTTP {Status}: {Details}", resp.StatusCode, details);
            throw new InvalidOperationException(
                $"Keycloak user creation failed (HTTP {resp.StatusCode}): {details}");
        }
        else
        {
            keycloakId = resp.Headers.Location?.ToString().Split('/').Last();
            _logger.LogInformation("Created Keycloak user '{Username}' with ID '{KeycloakId}'.", username, keycloakId);
        }

        if (keycloakId is not null)
            await ClearRequiredActionsAsync(http, keycloakId);

        return keycloakId;
    }

    public async Task<bool> SetUserPasswordAsync(string keycloakUserId, string password)
    {
        if (string.IsNullOrEmpty(keycloakUserId)) return false;
        var http = await _tokenClient.CreateAuthorizedClientAsync();
        return await SetUserPasswordAsync(http, keycloakUserId, password);
    }

    private async Task<bool> SetUserPasswordAsync(HttpClient http, string keycloakUserId, string password)
    {
        try
        {
            var url  = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users/{keycloakUserId}/reset-password";
            var body = new { type = "password", value = password, temporary = false };
            var resp = await http.PutAsync(url, JsonContent(body));

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to set password for Keycloak user '{UserId}': {Error}", keycloakUserId, err);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception setting password for Keycloak user '{UserId}'", keycloakUserId);
            return false;
        }
    }

    public async Task<bool> ClearRequiredActionsAsync(string keycloakUserId)
    {
        if (string.IsNullOrEmpty(keycloakUserId)) return false;
        var http = await _tokenClient.CreateAuthorizedClientAsync();
        return await ClearRequiredActionsAsync(http, keycloakUserId);
    }

    private async Task<bool> ClearRequiredActionsAsync(HttpClient http, string keycloakUserId)
    {
        try
        {
            var userUrl = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users/{keycloakUserId}";

            // GET current user — extract only safe mutable fields
            var getResp = await http.GetAsync(userUrl);
            if (!getResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("ClearRequiredActions: GET failed for '{UserId}' — HTTP {Status}",
                    keycloakUserId, (int)getResp.StatusCode);
                return false;
            }

            var existing = await ReadUserDocAsync(getResp);
            var payload  = BuildMinimalUserPayload(existing, keycloakUserId, requiredActions: Array.Empty<string>());

            // PUT minimal safe payload (read-only fields excluded to avoid 400)
            var putResp = await http.PutAsync(userUrl, JsonContent(payload));
            if (!putResp.IsSuccessStatusCode)
            {
                var err = await putResp.Content.ReadAsStringAsync();
                _logger.LogError("ClearRequiredActions: PUT failed for '{UserId}' — HTTP {Status}: {Error}",
                    keycloakUserId, (int)putResp.StatusCode, err);
                return false;
            }

            await LogVerificationAsync(http, userUrl, keycloakUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClearRequiredActions: unexpected error for user '{UserId}'", keycloakUserId);
            return false;
        }
    }

    private async Task UpdateUserProfileAsync(
        HttpClient http, string keycloakUserId,
        string username, string firstName, string lastName,
        string email, string userType)
    {
        try
        {
            var userUrl = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users/{keycloakUserId}";
            var getResp = await http.GetAsync(userUrl);
            if (!getResp.IsSuccessStatusCode) return;

            var existing = await ReadUserDocAsync(getResp);

            // Use supplied value if non-empty, otherwise keep existing
            string Merge(string supplied, string key) =>
                !string.IsNullOrWhiteSpace(supplied) ? supplied : GetString(existing, key);

            var payload = new Dictionary<string, object?>
            {
                ["id"]              = keycloakUserId,
                ["username"]        = GetString(existing, "username"),
                ["email"]           = Merge(email, "email"),
                ["firstName"]       = Merge(firstName, "firstName"),
                ["lastName"]        = Merge(lastName, "lastName"),
                ["enabled"]         = true,
                ["emailVerified"]   = true,
                ["requiredActions"] = Array.Empty<string>(),
                ["attributes"]      = new Dictionary<string, string[]>
                {
                    ["userType"] = new[] { userType }
                }
            };

            var putResp = await http.PutAsync(userUrl, JsonContent(payload));
            if (putResp.IsSuccessStatusCode)
                _logger.LogInformation(
                    "Updated Keycloak profile for '{Username}' (firstName='{First}', lastName='{Last}').",
                    username, payload["firstName"], payload["lastName"]);
            else
                _logger.LogWarning("UpdateUserProfile failed for '{UserId}' — HTTP {Status}: {Error}",
                    keycloakUserId, (int)putResp.StatusCode, await putResp.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateUserProfile: unexpected error for user '{UserId}'", keycloakUserId);
        }
    }

    public async Task AddUserToGroupAsync(string keycloakUserId, string groupName)
    {
        try
        {
            var http    = await _tokenClient.CreateAuthorizedClientAsync();
            var groupId = await EnsureGroupExistsAsync(http, groupName);

            if (groupId is null)
            {
                _logger.LogWarning(
                    "Cannot add user '{UserId}' to group '{Group}' — group ID could not be resolved.",
                    keycloakUserId, groupName);
                return;
            }

            var url  = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users/{keycloakUserId}/groups/{groupId}";
            var resp = await http.PutAsync(url, null);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Failed to add user '{UserId}' to group '{Group}' — HTTP {Status}",
                    keycloakUserId, groupName, resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user '{UserId}' to group '{Group}'", keycloakUserId, groupName);
        }
    }

    public async Task<string?> EnsureGroupExistsAndGetIdAsync(string groupName)
    {
        try
        {
            var http = await _tokenClient.CreateAuthorizedClientAsync();
            return await EnsureGroupExistsAsync(http, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure group '{Group}' exists in Keycloak", groupName);
            return null;
        }
    }

    private async Task<string?> EnsureGroupExistsAsync(HttpClient http, string groupName)
    {
        var searchUrl = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/groups?search={Uri.EscapeDataString(groupName)}";
        var searchResp = await http.GetAsync(searchUrl);
        if (searchResp.IsSuccessStatusCode)
        {
            var groups = await searchResp.Content.ReadFromJsonAsync<JsonElement>();
            var existing = FindGroupByName(groups, groupName);
            if (existing is not null) return existing;
        }

        // Create it
        var createResp = await http.PostAsync(
            $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/groups",
            JsonContent(new { name = groupName }));

        if (createResp.IsSuccessStatusCode || createResp.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var location = createResp.Headers.Location?.ToString();
            if (!string.IsNullOrEmpty(location)) return location.Split('/').Last();

            // Re-search after create
            var retryResp = await http.GetAsync(searchUrl);
            if (retryResp.IsSuccessStatusCode)
            {
                var groups = await retryResp.Content.ReadFromJsonAsync<JsonElement>();
                return FindGroupByName(groups, groupName);
            }
        }
        return null;
    }


    public static string GetGroupNameForUserType(string userType) =>
        (userType ?? string.Empty).Trim().ToLower() switch
        {
            "corporate" => "Corporate-Users",
            "mis"       => "MIS-Users",
            _           => "MyBdjobs-Users"
        };

    private static string? FindGroupByName(JsonElement groups, string name)
    {
        if (groups.ValueKind != JsonValueKind.Array) return null;
        foreach (var g in groups.EnumerateArray())
            if (string.Equals(g.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
                return g.GetProperty("id").GetString();
        return null;
    }

    private static async Task<Dictionary<string, JsonElement>> ReadUserDocAsync(HttpResponseMessage resp)
    {
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
               ?? new Dictionary<string, JsonElement>();
    }

    private static string GetString(Dictionary<string, JsonElement> doc, string key) =>
        doc.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static Dictionary<string, object?> BuildMinimalUserPayload(
        Dictionary<string, JsonElement> existing, string keycloakUserId, string[] requiredActions)
    {
        var payload = new Dictionary<string, object?>
        {
            ["id"]              = keycloakUserId,
            ["username"]        = GetString(existing, "username"),
            ["email"]           = GetString(existing, "email"),
            ["firstName"]       = GetString(existing, "firstName"),
            ["lastName"]        = GetString(existing, "lastName"),
            ["enabled"]         = true,
            ["emailVerified"]   = true,
            ["requiredActions"] = requiredActions,
        };
        if (existing.TryGetValue("attributes", out var attrs) && attrs.ValueKind != JsonValueKind.Null)
            payload["attributes"] = JsonSerializer.Deserialize<object>(attrs.GetRawText());
        return payload;
    }

    private static object BuildCreateUserBody(
        string username, string email, string firstName, string lastName,
        string password, string userType) => new
    {
        username,
        email,
        firstName,
        lastName,
        enabled         = true,
        emailVerified   = true,
        requiredActions = Array.Empty<string>(),
        attributes      = new Dictionary<string, string[]> { ["userType"] = new[] { userType } },
        credentials     = new[] { new { type = "password", value = password, temporary = false } }
    };

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private async Task LogVerificationAsync(HttpClient http, string userUrl, string keycloakUserId)
    {
        var verifyResp = await http.GetAsync(userUrl);
        if (!verifyResp.IsSuccessStatusCode) return;

        var doc = await ReadUserDocAsync(verifyResp);
        if (!doc.TryGetValue("requiredActions", out var ra)) return;

        var actions = ra.ValueKind == JsonValueKind.Array
            ? string.Join(", ", ra.EnumerateArray().Select(x => x.GetString()))
            : ra.ToString();

        if (string.IsNullOrWhiteSpace(actions))
            _logger.LogInformation(
                "ClearRequiredActions: verified — requiredActions is empty for user '{UserId}'.", keycloakUserId);
        else
            _logger.LogError(
                "ClearRequiredActions: requiredActions still contains [{Actions}] for user '{UserId}'. " +
                "Admin token may lack 'manage-users' permission.", actions, keycloakUserId);
    }
}
