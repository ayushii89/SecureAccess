using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Domain;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(u => u.Email).IsUnique();
            b.HasOne(u => u.Organization).WithMany(o => o.Users).HasForeignKey(u => u.OrganizationId);
            // Same null-passthrough filter as Role/AuditLog: unauthenticated requests (e.g. login,
            // which looks a user up by email before an org context exists) see all rows; authenticated
            // requests are scoped to the caller's org.
            b.HasQueryFilter(u => _tenantContext.CurrentOrganizationId == null || u.OrganizationId == _tenantContext.CurrentOrganizationId);
        });

        modelBuilder.Entity<Role>(b =>
        {
            b.HasIndex(r => new { r.OrganizationId, r.Name }).IsUnique();
            b.HasOne(r => r.Organization).WithMany(o => o.Roles).HasForeignKey(r => r.OrganizationId);
            b.HasQueryFilter(r => _tenantContext.CurrentOrganizationId == null || r.OrganizationId == _tenantContext.CurrentOrganizationId);
        });

        modelBuilder.Entity<Permission>(b =>
        {
            b.HasIndex(p => p.Name).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(b =>
        {
            b.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            b.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
            b.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId);
        });

        modelBuilder.Entity<UserRole>(b =>
        {
            b.HasKey(ur => new { ur.UserId, ur.RoleId });
            b.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
            b.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
        });

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasOne(rt => rt.User).WithMany(u => u.RefreshTokens).HasForeignKey(rt => rt.UserId);
            b.HasIndex(rt => rt.TokenHash).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.HasQueryFilter(a => _tenantContext.CurrentOrganizationId == null || a.OrganizationId == _tenantContext.CurrentOrganizationId);
        });
    }
}
