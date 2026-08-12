using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SecureAccess.Api.Data;

namespace SecureAccess.Api.Authorization;

// Resolves the caller's roles (from JWT role claims, scoped to the org in the org_id claim via
// AppDbContext's tenant query filter) to permissions and checks the requirement against them.
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AppDbContext _db;

    public PermissionHandler(AppDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roleNames = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roleNames.Count == 0)
        {
            return;
        }

        var hasPermission = await _db.Roles
            .Where(r => roleNames.Contains(r.Name))
            .SelectMany(r => r.RolePermissions)
            .AnyAsync(rp => rp.Permission!.Name == requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
