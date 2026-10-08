using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Services.Keycloak;

namespace UserLoginApp.Api.Services;

public interface IKeycloakAdminService
{
    Task<string?> GetUserIdByUsernameAsync(string username);
    Task<string?> CreateUserAsync(string username, string email, string firstName, string lastName, string password, string userType = "mybdjobs");
    Task AddUserToGroupAsync(string keycloakUserId, string groupName);
    Task<string?> EnsureGroupExistsAndGetIdAsync(string groupName);
    Task<bool> SetUserPasswordAsync(string keycloakUserId, string password);
    Task<bool> ClearRequiredActionsAsync(string keycloakUserId);
    Task<TokenResponse?> LoginUserAsync(string username, string password);
    Task<TokenResponse?> RefreshTokenAsync(string refreshToken);
}

public sealed class KeycloakAdminService : IKeycloakAdminService
{
    private readonly KeycloakTokenClient _tokenClient;
    private readonly KeycloakUserClient _userClient;
    private readonly ILogger<KeycloakAdminService> _logger;

    public KeycloakAdminService(IConfiguration config, ILogger<KeycloakAdminService> logger)
    {
        _logger = logger;
        var cfg = KeycloakConfig.FromConfiguration(config);
        _tokenClient = new KeycloakTokenClient(cfg, logger);
        _userClient  = new KeycloakUserClient(cfg, _tokenClient, logger);
    }

    public static string GetGroupNameForUserType(string userType) =>
        KeycloakUserClient.GetGroupNameForUserType(userType);

    // ── Token Operations ─────────────────────────────────────────────────────

    public async Task<TokenResponse?> LoginUserAsync(string username, string password)
        => await _tokenClient.LoginUserAsync(username, password);

    public Task<TokenResponse?> RefreshTokenAsync(string refreshToken)
        => _tokenClient.RefreshTokenAsync(refreshToken);

    // ── User Management ──────────────────────────────────────────────────────

    public Task<string?> GetUserIdByUsernameAsync(string username)
        => _userClient.GetUserIdByUsernameAsync(username);

    public async Task<string?> CreateUserAsync(
        string username, string email, string firstName, string lastName,
        string password, string userType = "mybdjobs")
    {
        try
        {
            var normalizedUserType = NormalizeUserType(userType);
            var keycloakId = await _userClient.CreateOrSyncUserAsync(
                username, email, firstName, lastName, password, normalizedUserType);

            if (keycloakId is not null)
            {
                var groupName = KeycloakUserClient.GetGroupNameForUserType(normalizedUserType);
                await _userClient.AddUserToGroupAsync(keycloakId, groupName);
            }

            return keycloakId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateUserAsync failed for user '{Username}'", username);
            throw new InvalidOperationException($"Keycloak user sync failed: {ex.Message}", ex);
        }
    }

    public Task<bool> SetUserPasswordAsync(string keycloakUserId, string password)
        => _userClient.SetUserPasswordAsync(keycloakUserId, password);

    public Task<bool> ClearRequiredActionsAsync(string keycloakUserId)
        => _userClient.ClearRequiredActionsAsync(keycloakUserId);

    // ── Group Management ─────────────────────────────────────────────────────

    public Task AddUserToGroupAsync(string keycloakUserId, string groupName)
        => _userClient.AddUserToGroupAsync(keycloakUserId, groupName);

    public Task<string?> EnsureGroupExistsAndGetIdAsync(string groupName)
        => _userClient.EnsureGroupExistsAndGetIdAsync(groupName);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string NormalizeUserType(string userType) =>
        (userType ?? string.Empty).Trim().ToLower() switch
        {
            "corporate" => "corporate",
            "mis"       => "mis",
            _           => "mybdjobs"
        };
}
