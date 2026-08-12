namespace SecureAccess.Api.Domain;

// Global catalog, not tenant-scoped — e.g. "users:create", "roles:manage", "audit:read"
public class Permission
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
