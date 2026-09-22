using Microsoft.EntityFrameworkCore;
using UserLoginApp.Api.Models.Entities;

namespace UserLoginApp.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<RoleFeature> RoleFeatures => Set<RoleFeature>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);

        modelBuilder.Entity<RoleFeature>()
            .HasKey(rf => new { rf.RoleId, rf.FeatureId });

        modelBuilder.Entity<RoleFeature>()
            .HasOne(rf => rf.Role)
            .WithMany(r => r.RoleFeatures)
            .HasForeignKey(rf => rf.RoleId);

        modelBuilder.Entity<RoleFeature>()
            .HasOne(rf => rf.Feature)
            .WithMany(f => f.RoleFeatures)
            .HasForeignKey(rf => rf.FeatureId);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<Feature>()
            .HasIndex(f => f.Code)
            .IsUnique();
    }
}
