using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Data;
using SecureAccess.Api.Domain;
using SecureAccess.Api.Services;

namespace SecureAccess.Api.Features.Auth;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;

    public AuthController(AppDbContext db, ITokenService tokenService, IAuditService auditService)
    {
        _db = db;
        _tokenService = tokenService;
        _auditService = auditService;
    }

    [HttpPost("register")]
    [EnableRateLimiting("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email, ct))
        {
            return Conflict("A user with this email already exists.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var organization = new Organization { Id = Guid.NewGuid(), Name = request.OrganizationName };
        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync(ct);

        var adminRole = await SeedData.CreateDefaultRolesAsync(_db, organization.Id, ct);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            OrganizationId = organization.Id,
        };
        _db.Users.Add(user);
        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        await _auditService.LogAsync(organization.Id, user.Id, "organization.registered", new { organization.Name, user.Email }, ct);

        var tokens = await _tokenService.IssueTokenPairAsync(user, new[] { "Admin" }, ct);
        return Ok(new AuthResponse(tokens.AccessToken, tokens.RefreshToken, tokens.RefreshTokenExpiresAt));
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            if (user is not null)
            {
                await _auditService.LogAsync(user.OrganizationId, user.Id, "auth.login.failed", ct: ct);
            }
            return Unauthorized("Invalid email or password.");
        }

        var roleNames = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role!.Name)
            .ToListAsync(ct);

        await _auditService.LogAsync(user.OrganizationId, user.Id, "auth.login.success", ct: ct);

        var tokens = await _tokenService.IssueTokenPairAsync(user, roleNames, ct);
        return Ok(new AuthResponse(tokens.AccessToken, tokens.RefreshToken, tokens.RefreshTokenExpiresAt));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var tokens = await _tokenService.RotateRefreshTokenAsync(request.RefreshToken, ct);
        if (tokens is null)
        {
            return Unauthorized("Invalid or expired refresh token.");
        }
        return Ok(new AuthResponse(tokens.AccessToken, tokens.RefreshToken, tokens.RefreshTokenExpiresAt));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        return NoContent();
    }
}
