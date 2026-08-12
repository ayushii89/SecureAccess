using System.Text.Json;
using SecureAccess.Api.Data;
using SecureAccess.Api.Domain;

namespace SecureAccess.Api.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(Guid organizationId, Guid? userId, string eventType, object? metadata = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            EventType = eventType,
            Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata),
        });
        await _db.SaveChangesAsync(ct);
    }
}
