using Microsoft.EntityFrameworkCore;
using UserLoginApp.Api.Models.Entities;
using UserLoginApp.Api.Services;

namespace UserLoginApp.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IKeycloakAdminService keycloak)
    {
        await db.Database.MigrateAsync();

        if (await db.Features.AnyAsync()) return;

        var features = new List<Feature>
        {
            new() { Id = Guid.NewGuid(), Name = "User Management", Code = "USER_MANAGEMENT", Description = "Create, update, delete and manage users" },
            new() { Id = Guid.NewGuid(), Name = "Role Management", Code = "ROLE_MANAGEMENT", Description = "Create roles and assign features" }
        };
        await db.Features.AddRangeAsync(features);

        var featureMap = features.ToDictionary(f => f.Code);

        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Description = "System administrator" };
        var myBdJobsRole = new Role { Id = Guid.NewGuid(), Name = "MyBdJobsUser", Description = "MyBdJobs job seeker user role" };
        var corporateRole = new Role { Id = Guid.NewGuid(), Name = "CorporateUser", Description = "Corporate employer user role" };

        await db.Roles.AddRangeAsync(adminRole, myBdJobsRole, corporateRole);

        db.RoleFeatures.AddRange(
            new RoleFeature { RoleId = adminRole.Id, FeatureId = featureMap["USER_MANAGEMENT"].Id },
            new RoleFeature { RoleId = adminRole.Id, FeatureId = featureMap["ROLE_MANAGEMENT"].Id }
        );

        const string adminPassword = "Admin@123";
        string? keycloakId = null;
        try
        {
            keycloakId = await keycloak.CreateUserAsync("admin", "admin@app.com", "System", "Admin", adminPassword);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Seeder] Keycloak sync warning: {ex.Message}");
        }

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@app.com",
            FirstName = "System",
            LastName = "Admin",
            UserType = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            KeycloakId = keycloakId,
            IsActive = true
        };
        await db.Users.AddAsync(adminUser);
        await db.UserRoles.AddAsync(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });

        await db.SaveChangesAsync();
    }
}
