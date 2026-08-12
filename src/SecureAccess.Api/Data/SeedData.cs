using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Domain;

namespace SecureAccess.Api.Data;

public static class SeedData
{
    public static readonly string[] PermissionCatalog =
    {
        "users:create", "users:read", "users:delete", "users:manage_roles",
        "roles:manage",
        "audit:read",
        "projects:read",
    };

    public static readonly IReadOnlyDictionary<string, string[]> DefaultRolePermissions = new Dictionary<string, string[]>
    {
        ["Admin"] = PermissionCatalog,
        ["Manager"] = new[] { "users:read", "audit:read", "projects:read" },
        ["Developer"] = new[] { "projects:read" },
        ["Intern"] = new[] { "projects:read" },
    };

    // Idempotent: safe to call on every startup.
    public static async Task EnsurePermissionCatalogAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Permissions.Select(p => p.Name).ToListAsync(ct);
        foreach (var name in PermissionCatalog.Except(existing))
        {
            db.Permissions.Add(new Permission { Id = Guid.NewGuid(), Name = name });
        }
        await db.SaveChangesAsync(ct);
    }

    // Creates the starter role set (Admin, Manager, Developer, Intern) for a newly registered organization.
    public static async Task<Role> CreateDefaultRolesAsync(AppDbContext db, Guid organizationId, CancellationToken ct = default)
    {
        var permissions = await db.Permissions.ToDictionaryAsync(p => p.Name, ct);
        Role? adminRole = null;

        foreach (var (roleName, permissionNames) in DefaultRolePermissions)
        {
            var role = new Role { Id = Guid.NewGuid(), Name = roleName, OrganizationId = organizationId };
            db.Roles.Add(role);

            foreach (var permName in permissionNames)
            {
                if (permissions.TryGetValue(permName, out var perm))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
                }
            }

            if (roleName == "Admin")
            {
                adminRole = role;
            }
        }

        await db.SaveChangesAsync(ct);
        return adminRole!;
    }
}
