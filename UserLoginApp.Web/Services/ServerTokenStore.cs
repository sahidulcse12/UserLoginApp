using System.Collections.Concurrent;

namespace UserLoginApp.Web.Services;

// Singleton bridge: stores access tokens keyed by Keycloak sub (user ID).
// Populated during the HTTP pre-render (_Host.cshtml) where HttpContext is available.
// Read by TokenCircuitHandler when the SignalR circuit opens in a new DI scope.
public class ServerTokenStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();

    public void Set(string userId, string token) => _store[userId] = token;

    public string? Get(string userId) =>
        _store.TryGetValue(userId, out var token) ? token : null;

    public void Remove(string userId) => _store.TryRemove(userId, out _);
}
