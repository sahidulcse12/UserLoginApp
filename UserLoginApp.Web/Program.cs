using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using UserLoginApp.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Cookie + Keycloak OIDC authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "userloginapp.auth";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
})
.AddOpenIdConnect(options =>
{
    options.Authority = builder.Configuration["Keycloak:Authority"];
    options.ClientId = builder.Configuration["Keycloak:ClientId"];
    options.ClientSecret = builder.Configuration["Keycloak:ClientSecret"];
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = true;          // stores access_token in auth cookie properties
    options.RequireHttpsMetadata = false;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.MapInboundClaims = false;

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");

    options.TokenValidationParameters.NameClaimType = "preferred_username";

    // Keycloak 26 advertises PAR support but .NET 9's OIDC handler fails when the
    // client isn't explicitly configured for PAR in Keycloak — disable it here.
    options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

    // After logout, return to app root
    options.SignedOutRedirectUri = "/";
});

builder.Services.AddAuthorization();

// Singleton bridge: stores tokens during HTTP pre-render, read by TokenCircuitHandler in circuit scope
builder.Services.AddSingleton<ServerTokenStore>();

// TokenProvider: scoped per circuit, populated by TokenCircuitHandler when SignalR connects
builder.Services.AddScoped<TokenProvider>();
builder.Services.AddScoped<CircuitHandler, TokenCircuitHandler>();

// UserContext: scoped per circuit, populated in MainLayout from /api/me
builder.Services.AddScoped<UserContext>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured.");

builder.Services.AddScoped<ApiService>(sp =>
{
    var tokenProvider = sp.GetRequiredService<TokenProvider>();
    return new ApiService(apiBaseUrl, tokenProvider);
});

// IHttpContextAccessor is needed to read tokens in _Host.cshtml
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Explicit login challenge endpoint — Blazor's RedirectToLogin navigates here with forceLoad
app.MapGet("/login", async context =>
{
    if (!context.User.Identity!.IsAuthenticated)
        await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme,
            new AuthenticationProperties { RedirectUri = "/" });
    else
        context.Response.Redirect("/");
});

app.MapBlazorHub();
app.MapRazorPages(); // for /logout razor page
app.MapFallbackToPage("/_Host");

await app.RunAsync();
