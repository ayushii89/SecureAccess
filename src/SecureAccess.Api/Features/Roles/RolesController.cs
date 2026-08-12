using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Data;
using SecureAccess.Api.Domain;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Features.Roles;

[ApiController]
[Route("roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;

    public RolesController(AppDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    private Guid CurrentOrgId => Guid.Parse(User.FindFirstValue("org_id")!);
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);

    [HttpGet]
    [Authorize(Policy = "roles:manage")]
    public async Task<ActionResult<IEnumerable<RoleResponse>>> List(CancellationToken ct)
    {
        var roles = await _db.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Select(r => new RoleResponse(r.Id, r.Name, r.RolePermissions.Select(rp => rp.Permission!.Name)))
            .ToListAsync(ct);
        return Ok(roles);
    }

    [HttpPost]
    [Authorize(Policy = "roles:manage")]
    public async Task<ActionResult<RoleResponse>> Create(CreateRoleRequest request, CancellationToken ct)
    {
        if (await _db.Roles.AnyAsync(r => r.Name == request.Name, ct))
        {
            return Conflict("A role with this name already exists in this organization.");
        }

        var permissions = await _db.Permissions
            .Where(p => request.PermissionNames.Contains(p.Name))
            .ToListAsync(ct);

        var role = new Role { Id = Guid.NewGuid(), Name = request.Name, OrganizationId = CurrentOrgId };
        _db.Roles.Add(role);
        foreach (var perm in permissions)
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
        }
        await _db.SaveChangesAsync(ct);

        await _auditService.LogAsync(CurrentOrgId, CurrentUserId, "role.created", new { role.Name, Permissions = permissions.Select(p => p.Name) }, ct);

        return Ok(new RoleResponse(role.Id, role.Name, permissions.Select(p => p.Name)));
    }

    [HttpPost("{roleId:guid}/permissions")]
    [Authorize(Policy = "roles:manage")]
    public async Task<IActionResult> AssignPermission(Guid roleId, AssignPermissionRequest request, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null)
        {
            return NotFound();
        }

        var permission = await _db.Permissions.FirstOrDefaultAsync(p => p.Name == request.PermissionName, ct);
        if (permission is null)
        {
            return BadRequest($"Unknown permission '{request.PermissionName}'.");
        }

        var alreadyAssigned = await _db.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permission.Id, ct);
        if (!alreadyAssigned)
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync(CurrentOrgId, CurrentUserId, "permission.granted", new { Role = role.Name, Permission = permission.Name }, ct);
        }

        return NoContent();
    }

    [HttpPost("assign")]
    [Authorize(Policy = "roles:manage")]
    public async Task<IActionResult> AssignRoleToUser(AssignRoleRequest request, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId, ct);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (role is null || user is null)
        {
            return NotFound();
        }

        var alreadyAssigned = await _db.UserRoles.AnyAsync(ur => ur.UserId == request.UserId && ur.RoleId == request.RoleId, ct);
        if (!alreadyAssigned)
        {
            _db.UserRoles.Add(new UserRole { UserId = request.UserId, RoleId = request.RoleId });
            await _db.SaveChangesAsync(ct);
            await _auditService.LogAsync(CurrentOrgId, CurrentUserId, "role.assigned", new { user.Email, Role = role.Name }, ct);
        }

        return NoContent();
    }
}
