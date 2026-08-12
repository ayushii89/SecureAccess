namespace SecureAccess.Api.Services;

public interface IAuditService
{
    Task LogAsync(Guid organizationId, Guid? userId, string eventType, object? metadata = null, CancellationToken ct = default);
}
