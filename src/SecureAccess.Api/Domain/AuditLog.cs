namespace SecureAccess.Api.Domain;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public required string EventType { get; set; }
    public string? Metadata { get; set; } // jsonb
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
