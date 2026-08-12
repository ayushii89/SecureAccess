using System.Net;
using System.Net.Http.Json;
using SecureAccess.Api.Features.Auth;
using SecureAccess.Api.Features.Roles;
using SecureAccess.Api.Tests.Infrastructure;

namespace SecureAccess.Api.Tests;

[Collection("Api")]
public class RbacTests
{
    private readonly ApiFactory _factory;

    public RbacTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Admin, string DeveloperEmail, string Password)> SetupOrgWithDeveloperAsync(string label)
    {
        var admin = _factory.CreateClient();
        var adminEmail = ApiClientExtensions.UniqueEmail($"{label}-admin");
        var adminAuth = await admin.RegisterOkAsync($"{label} Org", adminEmail);
        admin.AuthorizeWith(adminAuth.AccessToken);

        var roles = await admin.GetRolesAsync();
        var devRole = roles.Single(r => r.Name == "Developer");

        var devEmail = ApiClientExtensions.UniqueEmail($"{label}-dev");
        const string password = "P@ssw0rd123!";
        var createResponse = await admin.CreateUserAsync(devEmail, password, devRole.Id);
        createResponse.EnsureSuccessStatusCode();

        return (admin, devEmail, password);
    }

    [Fact]
    public async Task Admin_CanListRolesAndSeeStarterSet()
    {
        var admin = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("admin-roles");
        var auth = await admin.RegisterOkAsync("Acme", email);
        admin.AuthorizeWith(auth.AccessToken);

        var roles = await admin.GetRolesAsync();

        Assert.Equal(new[] { "Admin", "Manager", "Developer", "Intern" }.OrderBy(x => x), roles.Select(r => r.Name).OrderBy(x => x));
        Assert.Contains(roles, r => r.Name == "Admin" && r.Permissions.Contains("roles:manage"));
    }

    [Fact]
    public async Task DeveloperRole_Gets403_OnRolesManageEndpoint()
    {
        var (_, devEmail, password) = await SetupOrgWithDeveloperAsync("rbac-roles");
        var dev = _factory.CreateClient();
        var loginResponse = await dev.LoginAsync(devEmail, password);
        var devAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        dev.AuthorizeWith(devAuth!.AccessToken);

        var response = await dev.GetAsync("/roles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeveloperRole_Gets403_OnAuditLogEndpoint()
    {
        var (_, devEmail, password) = await SetupOrgWithDeveloperAsync("rbac-audit");
        var dev = _factory.CreateClient();
        var loginResponse = await dev.LoginAsync(devEmail, password);
        var devAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        dev.AuthorizeWith(devAuth!.AccessToken);

        var response = await dev.GetAsync("/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Request_Gets401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateCustomRole_AndAssignPermission()
    {
        var admin = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("admin-customrole");
        var auth = await admin.RegisterOkAsync("Acme", email);
        admin.AuthorizeWith(auth.AccessToken);

        var createResponse = await admin.PostAsJsonAsync("/roles", new CreateRoleRequest("Auditor", new[] { "audit:read" }));
        createResponse.EnsureSuccessStatusCode();

        var roles = await admin.GetRolesAsync();
        var auditor = roles.Single(r => r.Name == "Auditor");
        Assert.Contains("audit:read", auditor.Permissions);
    }
}
