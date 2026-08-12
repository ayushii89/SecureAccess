using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureAccess.Api.Data;
using SecureAccess.Api.Services;
using SecureAccess.Api.Tests.Infrastructure;

namespace SecureAccess.Api.Tests;

// Exercises the "find-or-create user from a verified email, issue tokens" logic that backs
// Google sign-in directly — the actual OAuth redirect/consent screen can't be driven in CI
// without live Google credentials, but this is the real business logic behind it, hitting
// the real test database via the same DI container the API uses.
[Collection("Api")]
public class ExternalAuthServiceTests
{
    private readonly ApiFactory _factory;

    public ExternalAuthServiceTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FirstLogin_CreatesOrganizationAndAdminUser_WithNoPassword()
    {
        var email = ApiClientExtensions.UniqueEmail("oauth-new");

        using var scope = _factory.Services.CreateScope();
        var externalAuth = scope.ServiceProvider.GetRequiredService<IExternalAuthService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tokens = await externalAuth.CompleteLoginAsync(email, "google");

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));

        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Null(user.PasswordHash);

        var roleNames = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role!.Name)
            .ToListAsync();
        Assert.Contains("Admin", roleNames);

        var organization = await db.Organizations.SingleAsync(o => o.Id == user.OrganizationId);
        Assert.Equal($"{email}'s Organization", organization.Name);
    }

    [Fact]
    public async Task SecondLogin_SameEmail_ReusesExistingUser_NoDuplicateOrg()
    {
        var email = ApiClientExtensions.UniqueEmail("oauth-repeat");

        using var scope = _factory.Services.CreateScope();
        var externalAuth = scope.ServiceProvider.GetRequiredService<IExternalAuthService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await externalAuth.CompleteLoginAsync(email, "google");
        var firstUserId = (await db.Users.SingleAsync(u => u.Email == email)).Id;

        await externalAuth.CompleteLoginAsync(email, "google");

        var users = await db.Users.Where(u => u.Email == email).ToListAsync();
        Assert.Single(users);
        Assert.Equal(firstUserId, users[0].Id);

        var orgCount = await db.Organizations.CountAsync(o => o.Name == $"{email}'s Organization");
        Assert.Equal(1, orgCount);
    }
}
