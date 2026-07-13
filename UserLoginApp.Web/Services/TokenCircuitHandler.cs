using Microsoft.AspNetCore.Components.Server.Circuits;

namespace UserLoginApp.Web.Services;

public class TokenCircuitHandler : CircuitHandler
{
    private readonly TokenProvider _tokenProvider;
    private readonly ServerTokenStore _tokenStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TokenCircuitHandler> _logger;

    public TokenCircuitHandler(
        TokenProvider tokenProvider,
        ServerTokenStore tokenStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<TokenCircuitHandler> logger)
    {
        _tokenProvider = tokenProvider;
        _tokenStore = tokenStore;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var ctx = _httpContextAccessor.HttpContext;
        var sub = ctx?.User.FindFirst("sub")?.Value;
        var token = sub != null ? _tokenStore.Get(sub) : null;

        _logger.LogInformation(
            "Circuit connected: HttpContext={HasCtx}, sub={Sub}, tokenInStore={HasToken}",
            ctx != null, sub ?? "null", token != null);

        if (token != null)
            _tokenProvider.AccessToken = token;

        return Task.CompletedTask;
    }
}
