using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Data;

namespace SecureAccess.Api.Features.AuditLogs;

[ApiController]
[Route("audit-logs")]
[Authorize(Policy = "audit:read")]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditLogsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLogResponse>>> List(CancellationToken ct)
    {
        // Scoped to the caller's org via AppDbContext's global query filter.
        var logs = await _db.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AuditLogResponse(a.Id, a.UserId, a.EventType, a.Metadata, a.CreatedAt))
            .ToListAsync(ct);
        return Ok(logs);
    }
}
