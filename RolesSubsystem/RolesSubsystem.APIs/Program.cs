using RolesSubsystem.APIs.Extensions;
using ServiceBus.Handlers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddKeycloakJwtAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddServiceBus();
builder.Services.AddRolesSubsystemServices();
builder.Services.AddCorsPolicy(builder.Configuration);

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger/index.html"));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
