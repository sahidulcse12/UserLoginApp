using AuthSubsystem.DTOs;
using AuthSubsystem.DTOs.Commands;
using AuthSubsystem.Handlers.Claims;
using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Handlers.Login;
using AuthSubsystem.Handlers.RefreshToken;
using AuthSubsystem.Handlers.Register;
using AuthSubsystem.Repositories;
using AuthSubsystem.Repositories.Abstractions;
using AuthSubsystem.Repositories.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SharedSubsystem.Handlers.Abstractions;

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
        // Enriches every authenticated request's ClaimsPrincipal with feature claims from DB
        services.AddScoped<IClaimsTransformation, FeatureClaimsTransformation>();

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IKeycloakAdminService, KeycloakAdminService>();

        services.AddScoped<ICommandHandler<RegisterUserCommand, AuthResult<RegisterResponse>>, RegisterUserCommandHandler>();
        services.AddScoped<ICommandHandler<LoginUserCommand, AuthResult<TokenResponse>>, LoginUserCommandHandler>();
        services.AddScoped<ICommandHandler<RefreshTokenCommand, AuthResult<TokenResponse>>, RefreshTokenCommandHandler>();
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
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200", "https://gateway.bdjobs.com" })
            .Select(o => o.TrimEnd('/'))
            .ToArray();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
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
