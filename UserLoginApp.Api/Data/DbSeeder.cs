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
            new() { Id = Guid.NewGuid(), Name = "User Management",     Code = "USER_MANAGEMENT",     Description = "Create, update, delete and manage users" },
            new() { Id = Guid.NewGuid(), Name = "Role Management",     Code = "ROLE_MANAGEMENT",     Description = "Create roles and assign features" },
            new() { Id = Guid.NewGuid(), Name = "Category Management", Code = "CATEGORY_MANAGEMENT", Description = "Create and manage categories" },
            new() { Id = Guid.NewGuid(), Name = "Product Management",  Code = "PRODUCT_MANAGEMENT",  Description = "Create and manage products" },
        };
        await db.Features.AddRangeAsync(features);

        var featureMap = features.ToDictionary(f => f.Code);

        var adminRole     = new Role { Id = Guid.NewGuid(), Name = "Admin",     Description = "System administrator" };
        var managerRole   = new Role { Id = Guid.NewGuid(), Name = "Manager",   Description = "Category manager" };
        var executiveRole = new Role { Id = Guid.NewGuid(), Name = "Executive", Description = "Product executive" };
        await db.Roles.AddRangeAsync(adminRole, managerRole, executiveRole);

        await db.RoleFeatures.AddRangeAsync(
            new() { RoleId = adminRole.Id,     FeatureId = featureMap["USER_MANAGEMENT"].Id },
            new() { RoleId = adminRole.Id,     FeatureId = featureMap["ROLE_MANAGEMENT"].Id },
            new() { RoleId = adminRole.Id,     FeatureId = featureMap["CATEGORY_MANAGEMENT"].Id },
            new() { RoleId = adminRole.Id,     FeatureId = featureMap["PRODUCT_MANAGEMENT"].Id },
            new() { RoleId = managerRole.Id,   FeatureId = featureMap["CATEGORY_MANAGEMENT"].Id },
            new() { RoleId = executiveRole.Id, FeatureId = featureMap["PRODUCT_MANAGEMENT"].Id }
        );

        // Sync admin user to Keycloak, store the returned Keycloak ID
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
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            KeycloakId = keycloakId,
            IsActive = true
        };
        await db.Users.AddAsync(adminUser);
        await db.UserRoles.AddAsync(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });

        await db.SaveChangesAsync();
    }
}
