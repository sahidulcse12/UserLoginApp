using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using AuthSubsystem.Handlers.Claims;
using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Handlers.Login;
using AuthSubsystem.Handlers.Logout;
using AuthSubsystem.Handlers.RefreshToken;
using AuthSubsystem.Handlers.Register;
using AuthSubsystem.Handlers.Roles;
using AuthSubsystem.Handlers.Sessions;
using AuthSubsystem.Repositories;
using AuthSubsystem.Repositories.Abstractions;
using AuthSubsystem.Repositories.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SharedSubsystem.Handlers.Abstructions;

namespace AuthSubsystem.APIs.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        return services;
    }

    public static IServiceCollection AddAuthSubsystemServices(this IServiceCollection services)
    {
        // Enriches every authenticated request's ClaimsPrincipal with feature claims & Keycloak roles
        services.AddScoped<IClaimsTransformation, FeatureClaimsTransformation>();

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IKeycloakAdminService, KeycloakAdminService>();

        // In-memory for a single instance; switch to a shared cache (e.g. Redis) when scaling out
        services.AddDistributedMemoryCache();
        services.AddSingleton<IRevokedSessionStore, DistributedRevokedSessionStore>();

        // Auth Command Handlers (ICommandHandler<TCommand>)
        services.AddScoped<ICommandHandler<RegisterUserCommand>, RegisterUserCommandHandler>();
        services.AddScoped<ICommandHandler<LoginUserCommand>, LoginUserCommandHandler>();
        services.AddScoped<ICommandHandler<RefreshTokenCommand>, RefreshTokenCommandHandler>();
        services.AddScoped<ICommandHandler<LogoutUserCommand>, LogoutUserCommandHandler>();

        // Dynamic Role & Permission Handlers
        services.AddScoped<IQueryHandler<GetAllUsersWithRolesQuery, IEnumerable<KeycloakUserRoleDto>>, GetAllUsersWithRolesQueryHandler>();
        services.AddScoped<IQueryHandler<GetUserPermissionsQuery, UserPermissionsDto?>, GetUserPermissionsQueryHandler>();
        services.AddScoped<ICommandHandler<AssignRoleCommand>, AssignRoleCommandHandler>();

        return services;
    }

    // Validate tokens issued by Keycloak (fetches JWKS automatically from Authority)
    public static IServiceCollection AddKeycloakJwtAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Keycloak:Authority"];
                options.Audience = configuration["Keycloak:ClientId"];
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.MapInboundClaims = false;     // keep claim names as-is from Keycloak
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,          // audience checked via Keycloak roles/scopes
                    NameClaimType = "preferred_username"
                };

                // Reject access tokens whose Keycloak session was ended through /api/auth/logout
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var sessionId = context.Principal?.FindFirst("sid")?.Value;
                        if (string.IsNullOrEmpty(sessionId)) return;

                        var revokedSessions = context.HttpContext.RequestServices.GetRequiredService<IRevokedSessionStore>();
                        if (await revokedSessions.IsRevokedAsync(sessionId, context.HttpContext.RequestAborted))
                            context.Fail("Session has been logged out.");
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[]
            {
                "http://localhost:4200",
                "http://localhost:3000",
                "http://localhost:5173",
                "https://gateway.bdjobs.com",
                "https://my.bdjobs.com",
                "https://corporate.bdjobs.com",
                "https://corporate3.bdjobs.com",
                "https://jobs.bdjobs.com",
                "https://bdjobs.com"
            };

        var allowedOrigins = new HashSet<string>(
            configuredOrigins.Select(o => o.TrimEnd('/')),
            StringComparer.OrdinalIgnoreCase);

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrWhiteSpace(origin))
                        return false;

                    // Direct match against explicitly configured origins
                    if (allowedOrigins.Contains(origin.TrimEnd('/')))
                        return true;

                    if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        var host = uri.Host;

                        // Allow all *.bdjobs.com and bdjobs.com domains
                        if (host.Equals("bdjobs.com", StringComparison.OrdinalIgnoreCase) ||
                            host.EndsWith(".bdjobs.com", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        // Allow localhost and 127.0.0.1 on any port for local development
                        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }

                    return false;
                })
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromHours(1));
            });
        });

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
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
        return services;
    }
}
