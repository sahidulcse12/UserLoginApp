using Microsoft.Extensions.Caching.Distributed;

namespace AuthSubsystem.Handlers.Sessions;

public class DistributedRevokedSessionStore : IRevokedSessionStore
{
    private const string KeyPrefix = "revoked-sid:";
    private readonly IDistributedCache _cache;

    public DistributedRevokedSessionStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public Task RevokeAsync(string sessionId, DateTimeOffset until, CancellationToken cancellationToken = default)
        => _cache.SetStringAsync(KeyPrefix + sessionId, "1",
            new DistributedCacheEntryOptions { AbsoluteExpiration = until }, cancellationToken);

    public async Task<bool> IsRevokedAsync(string sessionId, CancellationToken cancellationToken = default)
        => await _cache.GetStringAsync(KeyPrefix + sessionId, cancellationToken) is not null;
}
