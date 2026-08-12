using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Data;
using SecureAccess.Api.Domain;

namespace SecureAccess.Api.Services;

public class ExternalAuthService : IExternalAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;

    public ExternalAuthService(AppDbContext db, ITokenService tokenService, IAuditService auditService)
    {
        _db = db;
        _tokenService = tokenService;
        _auditService = auditService;
    }

    public async Task<TokenPair> CompleteLoginAsync(string email, string provider, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is not null)
        {
            var existingRoles = await _db.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.Role!.Name)
                .ToListAsync(ct);

            await _auditService.LogAsync(user.OrganizationId, user.Id, "auth.login.success", new { Provider = provider }, ct);
            return await _tokenService.IssueTokenPairAsync(user, existingRoles, ct);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var organization = new Organization { Id = Guid.NewGuid(), Name = $"{email}'s Organization" };
        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync(ct);

        var adminRole = await SeedData.CreateDefaultRolesAsync(_db, organization.Id, ct);

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = null,
            OrganizationId = organization.Id,
        };
        _db.Users.Add(newUser);
        _db.UserRoles.Add(new UserRole { UserId = newUser.Id, RoleId = adminRole.Id });
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        await _auditService.LogAsync(organization.Id, newUser.Id, "organization.registered", new { organization.Name, Email = email, Provider = provider }, ct);

        return await _tokenService.IssueTokenPairAsync(newUser, new[] { "Admin" }, ct);
    }
}
