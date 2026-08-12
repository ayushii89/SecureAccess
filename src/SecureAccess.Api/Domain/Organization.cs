namespace SecureAccess.Api.Domain;

public class Organization
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}
