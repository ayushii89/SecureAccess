namespace SecureAccess.Api.Domain;

public class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
