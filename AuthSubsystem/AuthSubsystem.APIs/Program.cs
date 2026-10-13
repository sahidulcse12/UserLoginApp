using AuthSubsystem.APIs.Extensions;
using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Handlers.Seeding;
using AuthSubsystem.Repositories.Data;
using DataSyncSubsystem.APIs.Extensions;
using Microsoft.OpenApi.Models;
using ServiceBus.Handlers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddKeycloakJwtAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddServiceBus();
builder.Services.AddAuthSubsystemServices();
// Composition-root exception: DataSync reacts to Auth events in-process
builder.Services.AddDataSyncSubsystem();
builder.Services.AddCorsPolicy(builder.Configuration);

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
    // Relative to /swagger/index.html, so it also resolves under the /KeycloakLogin gateway prefix
    c.SwaggerEndpoint("v1/swagger.json", "UserLoginApp API v1");
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
