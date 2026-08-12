namespace SecureAccess.Api.Features.AuditLogs;

public record AuditLogResponse(Guid Id, Guid? UserId, string EventType, string? Metadata, DateTimeOffset CreatedAt);
