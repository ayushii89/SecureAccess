using System.Security.Claims;

namespace SecureAccess.Api.Services;

public class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? CurrentOrganizationId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("org_id")?.Value;
            return Guid.TryParse(claim, out var orgId) ? orgId : null;
        }
    }
}
