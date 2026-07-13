using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using UserLoginApp.Api.Auth;
using UserLoginApp.Api.Data;
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
        options.RequireHttpsMetadata = false; // dev only
        options.MapInboundClaims = false;     // keep claim names as-is from Keycloak
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,          // audience checked via Keycloak roles/scopes
            NameClaimType = "preferred_username"
        };
    });

// Feature-based policies (features are enriched from app DB via IClaimsTransformation)
const string featureClaimType = "feature";
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UserManagement",     p => p.RequireClaim(featureClaimType, "USER_MANAGEMENT"));
    options.AddPolicy("RoleManagement",     p => p.RequireClaim(featureClaimType, "ROLE_MANAGEMENT"));
    options.AddPolicy("CategoryManagement", p => p.RequireClaim(featureClaimType, "CATEGORY_MANAGEMENT"));
    options.AddPolicy("ProductManagement",  p => p.RequireClaim(featureClaimType, "PRODUCT_MANAGEMENT"));
});

// Enriches every authenticated request's ClaimsPrincipal with feature claims from DB
builder.Services.AddScoped<IClaimsTransformation, FeatureClaimsTransformation>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IKeycloakAdminService, KeycloakAdminService>();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "UserLoginApp API v1"));

app.UseCors();
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
