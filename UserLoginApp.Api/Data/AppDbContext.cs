using Microsoft.EntityFrameworkCore;
using UserLoginApp.Api.Models.Entities;

namespace UserLoginApp.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("kc");

        modelBuilder.Ignore<UserRole>();
        modelBuilder.Ignore<Role>();
        modelBuilder.Ignore<RoleFeature>();
        modelBuilder.Ignore<Feature>();

        modelBuilder.Entity<User>()
            .ToTable("Users", "kc");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
    }

    public override int SaveChanges()
    {
        SetUpdatedAt();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetUpdatedAt();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetUpdatedAt()
    {
        var entries = ChangeTracker.Entries<User>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt ??= DateTime.UtcNow;
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
