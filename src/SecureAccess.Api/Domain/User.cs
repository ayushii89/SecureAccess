namespace SecureAccess.Api.Domain;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }

    // Null for OAuth-only accounts (e.g. Google sign-in) that never set a local password.
    public string? PasswordHash { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
