using Microsoft.AspNetCore.Authorization;
using SecureAccess.Api.Data;

namespace SecureAccess.Api.Authorization;

public static class AuthorizationExtensions
{
    // Registers one policy per permission in the catalog, named after the permission itself,
    // so endpoints can do [Authorize(Policy = "users:create")].
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (var permission in SeedData.PermissionCatalog)
        {
            options.AddPolicy(permission, policy => policy.Requirements.Add(new PermissionRequirement(permission)));
        }
    }
}
