using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using UserLoginApp.Api.Auth;
using UserLoginApp.Api.Data;
using UserLoginApp.Api.Data.Repositories;
using UserLoginApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UserLoginApp API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your Keycloak access token here"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Validate tokens issued by Keycloak (fetches JWKS automatically from Authority)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:ClientId"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;     // keep claim names as-is from Keycloak
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,          // audience checked via Keycloak roles/scopes
            NameClaimType = "preferred_username"
        };
    });

builder.Services.AddAuthorization();

// Enriches every authenticated request's ClaimsPrincipal with feature claims from DB
builder.Services.AddScoped<IClaimsTransformation, FeatureClaimsTransformation>();

builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IKeycloakAdminService, KeycloakAdminService>();

var allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200", "https://gateway.bdjobs.com" })
    .Select(o => o.TrimEnd('/'))
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Support reverse proxy headers (e.g. X-Forwarded-Proto, X-Forwarded-Host)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.All
});

// Support reverse proxy / gateway path prefix (e.g. /KeycloakLogin)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/KeycloakLogin", out var remaining))
    {
        context.Request.PathBase = "/KeycloakLogin";
        context.Request.Path = remaining;
    }
    await next();
});

// UseCors MUST be placed before UseSwagger so Swagger endpoints return CORS headers
app.UseCors();

app.UseSwagger(c =>
{
    c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
    {
        var serverUrl = $"{httpReq.Scheme}://{httpReq.Host.Value}{httpReq.PathBase.Value}";
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new OpenApiServer { Url = serverUrl }
        };
    });
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/KeycloakLogin/swagger/v1/swagger.json", "UserLoginApp API v1");
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "UserLoginApp API v1 (Local)");
    c.RoutePrefix = "swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger/index.html"));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var keycloak = scope.ServiceProvider.GetRequiredService<IKeycloakAdminService>();
    await DbSeeder.SeedAsync(db, keycloak);
}

await app.RunAsync();
