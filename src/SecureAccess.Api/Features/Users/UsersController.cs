using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Data;
using SecureAccess.Api.Domain;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Features.Users;

[ApiController]
[Route("users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;

    public UsersController(AppDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    private Guid CurrentOrgId => Guid.Parse(User.FindFirstValue("org_id")!);
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);

    [HttpGet]
    [Authorize(Policy = "users:read")]
    public async Task<ActionResult<IEnumerable<UserResponse>>> List(CancellationToken ct)
    {
        var users = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Select(u => new UserResponse(u.Id, u.Email, u.UserRoles.Select(ur => ur.Role!.Name)))
            .ToListAsync(ct);
        return Ok(users);
    }

    [HttpPost]
    [Authorize(Policy = "users:create")]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email, ct))
        {
            return Conflict("A user with this email already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            OrganizationId = CurrentOrgId,
        };
        _db.Users.Add(user);

        var roleNames = new List<string>();
        if (request.RoleId is { } roleId)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct);
            if (role is null)
            {
                return BadRequest("Unknown role.");
            }
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            roleNames.Add(role.Name);
        }

        await _db.SaveChangesAsync(ct);
        await _auditService.LogAsync(CurrentOrgId, CurrentUserId, "user.created", new { user.Email, Roles = roleNames }, ct);

        return Ok(new UserResponse(user.Id, user.Email, roleNames));
    }
}
