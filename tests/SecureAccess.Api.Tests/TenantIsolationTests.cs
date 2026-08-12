using System.Net.Http.Json;
using SecureAccess.Api.Features.Roles;
using SecureAccess.Api.Tests.Infrastructure;

namespace SecureAccess.Api.Tests;

[Collection("Api")]
public class TenantIsolationTests
{
    private readonly ApiFactory _factory;

    public TenantIsolationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SecondOrg_CannotSeeFirstOrgs_Roles()
    {
        var orgA = _factory.CreateClient();
        var authA = await orgA.RegisterOkAsync("Org A", ApiClientExtensions.UniqueEmail("orga-admin"));
        orgA.AuthorizeWith(authA.AccessToken);
        // Give Org A a distinctive custom role so leakage would be unambiguous.
        await orgA.PostAsJsonAsync("/roles", new CreateRoleRequest("OrgA-OnlyRole", new[] { "audit:read" }));

        var orgB = _factory.CreateClient();
        var authB = await orgB.RegisterOkAsync("Org B", ApiClientExtensions.UniqueEmail("orgb-admin"));
        orgB.AuthorizeWith(authB.AccessToken);

        var orgBRoles = await orgB.GetRolesAsync();

        Assert.DoesNotContain(orgBRoles, r => r.Name == "OrgA-OnlyRole");
        Assert.Equal(new[] { "Admin", "Manager", "Developer", "Intern" }.OrderBy(x => x), orgBRoles.Select(r => r.Name).OrderBy(x => x));
    }

    [Fact]
    public async Task SecondOrg_CannotSeeFirstOrgs_Users()
    {
        var orgA = _factory.CreateClient();
        var emailA = ApiClientExtensions.UniqueEmail("isoA-admin");
        var authA = await orgA.RegisterOkAsync("Org A", emailA);
        orgA.AuthorizeWith(authA.AccessToken);

        var orgB = _factory.CreateClient();
        var authB = await orgB.RegisterOkAsync("Org B", ApiClientExtensions.UniqueEmail("isoB-admin"));
        orgB.AuthorizeWith(authB.AccessToken);

        var orgBUsers = await orgB.GetUsersAsync();

        Assert.DoesNotContain(orgBUsers, u => u.Email == emailA);
        Assert.Single(orgBUsers);
    }

    [Fact]
    public async Task SecondOrg_CannotSeeFirstOrgs_AuditLogs()
    {
        var orgA = _factory.CreateClient();
        var authA = await orgA.RegisterOkAsync("Org A", ApiClientExtensions.UniqueEmail("auditA-admin"));
        orgA.AuthorizeWith(authA.AccessToken);
        var orgALogsBefore = await orgA.GetAuditLogsAsync();
        Assert.Contains(orgALogsBefore, l => l.EventType == "organization.registered");

        var orgB = _factory.CreateClient();
        var authB = await orgB.RegisterOkAsync("Org B", ApiClientExtensions.UniqueEmail("auditB-admin"));
        orgB.AuthorizeWith(authB.AccessToken);

        var orgBLogs = await orgB.GetAuditLogsAsync();

        // Org B should only ever see its own single registration event, never Org A's activity.
        Assert.Single(orgBLogs);
        Assert.Equal("organization.registered", orgBLogs[0].EventType);
    }

    [Fact]
    public async Task RoleAssignment_CannotTargetUserInAnotherOrg()
    {
        var orgA = _factory.CreateClient();
        var authA = await orgA.RegisterOkAsync("Org A", ApiClientExtensions.UniqueEmail("crossA-admin"));
        orgA.AuthorizeWith(authA.AccessToken);
        var rolesA = await orgA.GetRolesAsync();
        var adminRoleA = rolesA.Single(r => r.Name == "Admin");

        var orgB = _factory.CreateClient();
        var emailB = ApiClientExtensions.UniqueEmail("crossB-admin");
        var authB = await orgB.RegisterOkAsync("Org B", emailB);
        orgB.AuthorizeWith(authB.AccessToken);
        var orgBUsers = await orgB.GetUsersAsync();
        var orgBAdminUserId = orgBUsers.Single(u => u.Email == emailB).Id;

        // Org A admin tries to assign an Org A role to an Org B user id — the user lookup is
        // tenant-filtered, so Org A can't even resolve that id and the call must fail.
        var response = await orgA.PostAsJsonAsync("/roles/assign", new AssignRoleRequest(orgBAdminUserId, adminRoleA.Id));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
