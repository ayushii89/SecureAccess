namespace SecureAccess.Api.Services;

public interface ITenantContext
{
    // Null when the request is unauthenticated (e.g. register/login) or the token carries no org_id claim.
    Guid? CurrentOrganizationId { get; }
}
