using AuthSubsystem.Handlers.Keycloak;
using AuthSubsystem.Repositories.Data;

namespace AuthSubsystem.Handlers.Seeding;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IKeycloakAdminService keycloak)
    {
        // Role & Feature seeding bypassed for Phase 1 (Live DB only contains kc.Users table)
        await Task.CompletedTask;
    }
}
