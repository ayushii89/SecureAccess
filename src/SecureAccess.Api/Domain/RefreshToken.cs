namespace SecureAccess.Api.Domain;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }

    // Only the hash is stored; the raw token is returned to the client once and never persisted.
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
