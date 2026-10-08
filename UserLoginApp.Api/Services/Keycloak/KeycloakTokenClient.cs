using System.Text.Json;
using System.Net.Http.Headers;
using UserLoginApp.Api.Models.DTOs;

namespace UserLoginApp.Api.Services.Keycloak;

internal sealed class KeycloakTokenClient
{
    private readonly KeycloakConfig _cfg;
    private readonly ILogger _logger;

    public KeycloakTokenClient(KeycloakConfig cfg, ILogger logger)
    {
        _cfg = cfg;
        _logger = logger;
    }

    public async Task<string> GetAdminTokenAsync()
    {
        using var http = new HttpClient();
        var errors = new List<string>();

        // 1. Client Credentials (service account) — preferred
        if (!string.IsNullOrEmpty(_cfg.ClientSecret))
        {
            var token = await TryClientCredentialsAsync(http, errors);
            if (token is not null) return token;
        }

        // 2. Master realm password grant (admin-cli)
        var masterToken = await TryMasterRealmGrantAsync(http, errors);
        if (masterToken is not null) return masterToken;

        // 3. Fallback: target realm password grant
        if (!string.Equals(_cfg.AdminRealm, _cfg.Realm, StringComparison.OrdinalIgnoreCase))
        {
            var fallbackToken = await TryTargetRealmGrantAsync(http, errors);
            if (fallbackToken is not null) return fallbackToken;
        }

        throw new InvalidOperationException(
            $"Failed to obtain Keycloak admin token for client '{_cfg.ClientId}' / user '{_cfg.AdminUsername}' from '{_cfg.BaseUrl}'. " +
            $"Details: {string.Join(" | ", errors)}");
    }

    public async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        var token = await GetAdminTokenAsync();
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }


    public async Task<TokenResponse> LoginUserAsync(string username, string password)
    {
        using var http = new HttpClient();
        var tokenUrl = $"{_cfg.BaseUrl}/realms/{_cfg.Realm}/protocol/openid-connect/token";
        var form = BuildUserLoginForm(username, password);

        var response = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var reason = ParseKeycloakError(responseBody);
            _logger.LogWarning(
                "Keycloak login failed for '{Username}' — HTTP {Status}: {Reason}",
                username, (int)response.StatusCode, reason);
            throw new InvalidOperationException(
                $"Keycloak authentication failed for '{username}' (HTTP {(int)response.StatusCode}): {reason}");
        }

        return ParseTokenResponse(responseBody);
    }

    public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken)
    {
        using var http = new HttpClient();
        var tokenUrl = $"{_cfg.BaseUrl}/realms/{_cfg.Realm}/protocol/openid-connect/token";
        var form = BuildRefreshForm(refreshToken);

        try
        {
            var response = await http.PostAsync(tokenUrl, new FormUrlEncodedContent(form));
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Keycloak refresh failed — HTTP {Status}: {Error}", response.StatusCode, err);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            return ParseTokenResponse(body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while refreshing Keycloak token");
            return null;
        }
    }

    private async Task<string?> TryClientCredentialsAsync(HttpClient http, List<string> errors)
    {
        var url = $"{_cfg.BaseUrl}/realms/{_cfg.Realm}/protocol/openid-connect/token";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _cfg.ClientId,
            ["client_secret"] = _cfg.ClientSecret
        });
        try
        {
            var resp = await http.PostAsync(url, form);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("access_token", out var t)) return t.GetString();
            }
            errors.Add($"Client Credentials ({url}): HTTP {(int)resp.StatusCode} — {await resp.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex) { errors.Add($"Client Credentials exception: {ex.Message}"); }
        return null;
    }

    private async Task<string?> TryMasterRealmGrantAsync(HttpClient http, List<string> errors)
    {
        var url = $"{_cfg.BaseUrl}/realms/{_cfg.AdminRealm}/protocol/openid-connect/token";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = _cfg.AdminUsername,
            ["password"] = _cfg.AdminPassword
        });
        try
        {
            var resp = await http.PostAsync(url, form);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
                return json.GetProperty("access_token").GetString();
            }
            errors.Add($"Master Realm ({url}): HTTP {(int)resp.StatusCode} — {await resp.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex) { errors.Add($"Master Realm exception: {ex.Message}"); }
        return null;
    }

    private async Task<string?> TryTargetRealmGrantAsync(HttpClient http, List<string> errors)
    {
        var url = $"{_cfg.BaseUrl}/realms/{_cfg.Realm}/protocol/openid-connect/token";
        var formDict = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _cfg.ClientId,
            ["username"] = _cfg.AdminUsername,
            ["password"] = _cfg.AdminPassword
        };
        if (!string.IsNullOrEmpty(_cfg.ClientSecret))
            formDict["client_secret"] = _cfg.ClientSecret;
        try
        {
            var resp = await http.PostAsync(url, new FormUrlEncodedContent(formDict));
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
                return json.GetProperty("access_token").GetString();
            }
            errors.Add($"Target Realm ({url}): HTTP {(int)resp.StatusCode} — {await resp.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex) { errors.Add($"Target Realm exception: {ex.Message}"); }
        return null;
    }

    private Dictionary<string, string> BuildUserLoginForm(string username, string password)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _cfg.ClientId,
            ["username"] = username,
            ["password"] = password
        };
        if (!string.IsNullOrEmpty(_cfg.ClientSecret))
            form["client_secret"] = _cfg.ClientSecret;
        return form;
    }

    private Dictionary<string, string> BuildRefreshForm(string refreshToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _cfg.ClientId,
            ["refresh_token"] = refreshToken
        };
        if (!string.IsNullOrEmpty(_cfg.ClientSecret))
            form["client_secret"] = _cfg.ClientSecret;
        return form;
    }

    private static TokenResponse ParseTokenResponse(string body)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        return new TokenResponse
        {
            AccessToken = json.GetProperty("access_token").GetString()!,
            RefreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
            ExpiresIn = json.GetProperty("expires_in").GetInt32(),
            RefreshExpiresIn = json.TryGetProperty("refresh_expires_in", out var rei) ? rei.GetInt32() : 1800,
            TokenType = json.TryGetProperty("token_type", out var tt) ? tt.GetString() ?? "Bearer" : "Bearer"
        };
    }

    private static string ParseKeycloakError(string body)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(body);
            if (json.TryGetProperty("error_description", out var desc) && desc.GetString() is { } d)
                return d;
            if (json.TryGetProperty("error", out var err) && err.GetString() is { } e)
                return e;
        }
        catch { /* fall through */ }
        return body;
    }
}
