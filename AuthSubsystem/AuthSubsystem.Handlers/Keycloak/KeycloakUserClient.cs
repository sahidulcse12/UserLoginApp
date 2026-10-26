using Microsoft.Extensions.Logging;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;
using AuthSubsystem.DTOs;

namespace AuthSubsystem.Handlers.Keycloak;

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

            var getResp = await http.GetAsync(userUrl);
            if (!getResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("ClearRequiredActions: GET failed for '{UserId}' — HTTP {Status}",
                    keycloakUserId, (int)getResp.StatusCode);
                return false;
            }

            var existing = await ReadUserDocAsync(getResp);
            var payload  = BuildMinimalUserPayload(existing, keycloakUserId, requiredActions: Array.Empty<string>());

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

        var createResp = await http.PostAsync(
            $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/groups",
            JsonContent(new { name = groupName }));

        if (createResp.IsSuccessStatusCode || createResp.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var location = createResp.Headers.Location?.ToString();
            if (!string.IsNullOrEmpty(location)) return location.Split('/').Last();

            var retryResp = await http.GetAsync(searchUrl);
            if (retryResp.IsSuccessStatusCode)
            {
                var groups = await retryResp.Content.ReadFromJsonAsync<JsonElement>();
                return FindGroupByName(groups, groupName);
            }
        }
        return null;
    }

    // ── Dynamic Users & Roles Retrieval ──────────────────────────────────────

    public async Task<IEnumerable<KeycloakUserRoleDto>> GetAllUsersWithRolesAsync(string? filterUserType, CancellationToken cancellationToken)
    {
        var result = new List<KeycloakUserRoleDto>();
        try
        {
            var http = await _tokenClient.CreateAuthorizedClientAsync();
            var url = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users?max=1000";
            var resp = await http.GetAsync(url, cancellationToken);
            if (!resp.IsSuccessStatusCode) return result;

            var users = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (users.ValueKind != JsonValueKind.Array) return result;

            var targetGroup = !string.IsNullOrWhiteSpace(filterUserType) 
                ? GetGroupNameForUserType(filterUserType) 
                : null;

            foreach (var userEl in users.EnumerateArray())
            {
                var userDto = ParseKeycloakUser(userEl);
                if (userDto == null) continue;

                // Fetch user groups
                userDto.Groups = await GetUserGroupsAsync(http, userDto.Id, cancellationToken);

                // Filter by group/userType if specified
                if (!string.IsNullOrWhiteSpace(targetGroup) && !userDto.Groups.Contains(targetGroup, StringComparer.OrdinalIgnoreCase))
                {
                    // Also check userType attribute as fallback
                    if (!string.Equals(userDto.UserType, filterUserType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                // Fetch user realm roles
                userDto.Roles = await GetUserRealmRolesAsync(http, userDto.Id, cancellationToken);
                userDto.IsAdmin = userDto.IsAdmin || userDto.Roles.Contains("admin", StringComparer.OrdinalIgnoreCase);

                result.Add(userDto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users with roles from Keycloak");
        }
        return result;
    }

    public async Task<UserPermissionsDto?> GetUserPermissionsByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        try
        {
            var http = await _tokenClient.CreateAuthorizedClientAsync();
            var url = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users?username={Uri.EscapeDataString(username)}&exact=true";
            var resp = await http.GetAsync(url, cancellationToken);
            if (!resp.IsSuccessStatusCode) return null;

            var users = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (users.ValueKind != JsonValueKind.Array || users.GetArrayLength() == 0) return null;

            var userDto = ParseKeycloakUser(users[0]);
            if (userDto == null) return null;

            var groups = await GetUserGroupsAsync(http, userDto.Id, cancellationToken);
            var roles = await GetUserRealmRolesAsync(http, userDto.Id, cancellationToken);

            return new UserPermissionsDto
            {
                Username = userDto.Username,
                UserType = userDto.UserType,
                Groups = groups,
                Roles = roles,
                ModuleIds = userDto.ModuleIds,
                ViewAccess = userDto.ViewAccess,
                IsAdmin = userDto.IsAdmin || roles.Contains("admin", StringComparer.OrdinalIgnoreCase),
                SubLinkIds = userDto.SubLinkIds,
                DepartmentId = userDto.DepartmentId,
                AccessFromHome = userDto.AccessFromHome,
                CustomAttributes = userDto.CustomAttributes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting permissions for user '{Username}'", username);
            return null;
        }
    }

    public async Task<bool> AssignRoleAndAttributesAsync(AssignRoleRequest req, CancellationToken cancellationToken)
    {
        try
        {
            var http = await _tokenClient.CreateAuthorizedClientAsync();

            // 1. Find user
            var searchUrl = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users?username={Uri.EscapeDataString(req.Username)}&exact=true";
            var searchResp = await http.GetAsync(searchUrl, cancellationToken);
            if (!searchResp.IsSuccessStatusCode) return false;

            var users = await searchResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (users.ValueKind != JsonValueKind.Array || users.GetArrayLength() == 0) return false;

            var userEl = users[0];
            var userId = userEl.GetProperty("id").GetString()!;

            // 2. Ensure Realm Role exists & assign to user
            var roleName = req.Role.Trim().ToLowerInvariant();
            await EnsureRealmRoleExistsAsync(http, roleName, cancellationToken);
            await AssignRealmRoleToUserAsync(http, userId, roleName, cancellationToken);

            // 3. Sync to Group dynamically if UserType specified
            if (!string.IsNullOrWhiteSpace(req.UserType))
            {
                var groupName = GetGroupNameForUserType(req.UserType);
                await AddUserToGroupAsync(userId, groupName);
            }

            // 4. Update user attributes
            var userUrl = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users/{userId}";
            var getResp = await http.GetAsync(userUrl, cancellationToken);
            if (!getResp.IsSuccessStatusCode) return false;

            var existingDoc = await getResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(cancellationToken: cancellationToken)
                              ?? new Dictionary<string, JsonElement>();

            var existingAttrs = new Dictionary<string, string[]>();
            if (existingDoc.TryGetValue("attributes", out var attrsEl) && attrsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in attrsEl.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        existingAttrs[prop.Name] = prop.Value.EnumerateArray().Select(v => v.GetString() ?? "").ToArray();
                    }
                }
            }

            var isAdmin = string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase);
            existingAttrs["role"] = new[] { roleName };
            existingAttrs["AdminChk"] = new[] { isAdmin ? "1" : "0" };
            if (!string.IsNullOrWhiteSpace(req.UserType)) existingAttrs["userType"] = new[] { req.UserType.ToLowerInvariant() };
            if (req.ModuleIds != null) existingAttrs["ModuleAccess"] = new[] { string.Join(",", req.ModuleIds) };
            if (req.ViewAccess != null) existingAttrs["ViewAccess"] = new[] { req.ViewAccess };
            if (req.SubLinkIds != null) existingAttrs["SubLinkAccess"] = new[] { string.Join(",", req.SubLinkIds) };
            if (req.DepartmentId.HasValue) existingAttrs["DepId"] = new[] { req.DepartmentId.Value.ToString() };
            if (req.AccessFromHome.HasValue) existingAttrs["AccessFromHome"] = new[] { req.AccessFromHome.Value ? "1" : "0" };

            // Additional custom dynamic attributes
            if (req.AdditionalAttributes != null)
            {
                foreach (var (k, v) in req.AdditionalAttributes)
                {
                    existingAttrs[k] = new[] { v };
                }
            }

            var payload = new Dictionary<string, object?>
            {
                ["id"] = userId,
                ["username"] = userEl.GetProperty("username").GetString(),
                ["email"] = userEl.TryGetProperty("email", out var em) ? em.GetString() : null,
                ["firstName"] = userEl.TryGetProperty("firstName", out var fn) ? fn.GetString() : null,
                ["lastName"] = userEl.TryGetProperty("lastName", out var ln) ? ln.GetString() : null,
                ["enabled"] = true,
                ["attributes"] = existingAttrs
            };

            var putResp = await http.PutAsync(userUrl, JsonContent(payload), cancellationToken);
            return putResp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role & attributes for user '{Username}'", req.Username);
            return false;
        }
    }

    private async Task<List<string>> GetUserGroupsAsync(HttpClient http, string userId, CancellationToken cancellationToken)
    {
        var groups = new List<string>();
        try
        {
            var url = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users/{userId}/groups";
            var resp = await http.GetAsync(url, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var arr = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                if (arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in arr.EnumerateArray())
                    {
                        var name = g.GetProperty("name").GetString();
                        if (!string.IsNullOrEmpty(name)) groups.Add(name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get groups for user {UserId}", userId);
        }
        return groups;
    }

    private async Task<List<string>> GetUserRealmRolesAsync(HttpClient http, string userId, CancellationToken cancellationToken)
    {
        var roles = new List<string>();
        try
        {
            var url = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users/{userId}/role-mappings/realm";
            var resp = await http.GetAsync(url, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var arr = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                if (arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in arr.EnumerateArray())
                    {
                        var name = r.GetProperty("name").GetString();
                        if (!string.IsNullOrEmpty(name)) roles.Add(name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get realm roles for user {UserId}", userId);
        }
        return roles;
    }

    private async Task EnsureRealmRoleExistsAsync(HttpClient http, string roleName, CancellationToken cancellationToken)
    {
        var checkUrl = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/roles/{Uri.EscapeDataString(roleName)}";
        var resp = await http.GetAsync(checkUrl, cancellationToken);
        if (resp.IsSuccessStatusCode) return;

        var createUrl = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/roles";
        var payload = new { name = roleName, description = $"{roleName} role managed by system" };
        await http.PostAsync(createUrl, JsonContent(payload), cancellationToken);
    }

    private async Task AssignRealmRoleToUserAsync(HttpClient http, string userId, string roleName, CancellationToken cancellationToken)
    {
        var roleUrl = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/roles/{Uri.EscapeDataString(roleName)}";
        var roleResp = await http.GetAsync(roleUrl, cancellationToken);
        if (!roleResp.IsSuccessStatusCode) return;

        var roleObj = await roleResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var roleId = roleObj.GetProperty("id").GetString();
        var name = roleObj.GetProperty("name").GetString();

        var assignUrl = $"{_cfg.BaseUrl}/admin/realms/{_cfg.Realm}/users/{userId}/role-mappings/realm";
        var body = new[] { new { id = roleId, name = name } };
        await http.PostAsync(assignUrl, JsonContent(body), cancellationToken);
    }

    private static KeycloakUserRoleDto? ParseKeycloakUser(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;

        var user = new KeycloakUserRoleDto
        {
            Id = el.GetProperty("id").GetString() ?? string.Empty,
            Username = el.GetProperty("username").GetString() ?? string.Empty,
            Email = el.TryGetProperty("email", out var e) ? e.GetString() : null,
            FirstName = el.TryGetProperty("firstName", out var fn) ? fn.GetString() : null,
            LastName = el.TryGetProperty("lastName", out var ln) ? ln.GetString() : null
        };

        if (el.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
        {
            user.UserType = GetAttrFirst(attrs, "userType") ?? "mis";
            user.ViewAccess = GetAttrFirst(attrs, "ViewAccess") ?? GetAttrFirst(attrs, "viewAccess");

            var moduleAccess = GetAttrFirst(attrs, "ModuleAccess") ?? GetAttrFirst(attrs, "moduleAccess");
            user.ModuleIds = ParseIntList(moduleAccess);

            var subLinks = GetAttrFirst(attrs, "SubLinkAccess") ?? GetAttrFirst(attrs, "subLinkAccess");
            user.SubLinkIds = ParseIntList(subLinks);

            var adminChk = GetAttrFirst(attrs, "AdminChk") ?? GetAttrFirst(attrs, "adminChk");
            user.IsAdmin = adminChk == "1";

            var depId = GetAttrFirst(attrs, "DepId") ?? GetAttrFirst(attrs, "depId");
            if (int.TryParse(depId, out var did)) user.DepartmentId = did;

            var accessHome = GetAttrFirst(attrs, "AccessFromHome") ?? GetAttrFirst(attrs, "accessFromHome");
            user.AccessFromHome = accessHome == "1";

            foreach (var prop in attrs.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                {
                    user.CustomAttributes[prop.Name] = prop.Value[0].GetString() ?? "";
                }
            }
        }

        return user;
    }

    private static List<int> ParseIntList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<int>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(s => s.Trim())
                  .Where(s => int.TryParse(s, out _))
                  .Select(int.Parse)
                  .Distinct()
                  .ToList();
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

    private static string? GetAttrFirst(JsonElement attrs, string key)
    {
        if (attrs.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.Array && val.GetArrayLength() > 0)
        {
            return val[0].GetString();
        }
        return null;
    }

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
