using System.Net;
using System.Net.Http.Json;
using SecureAccess.Api.Features.Auth;
using SecureAccess.Api.Tests.Infrastructure;

namespace SecureAccess.Api.Tests;

[Collection("Api")]
public class AuthFlowTests
{
    private readonly ApiFactory _factory;

    public AuthFlowTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_CreatesOrgAndAdmin_ReturnsTokenPair()
    {
        var client = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("admin");

        var auth = await client.RegisterOkAsync("Acme", email);

        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.True(auth.RefreshTokenExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("dup");

        await client.RegisterOkAsync("Acme", email);
        var second = await client.RegisterAsync("Some Other Org", email);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_Succeeds()
    {
        var client = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("login-ok");
        await client.RegisterOkAsync("Acme", email, "correct-password");

        var response = await client.LoginAsync(email, "correct-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized_AndIsAudited()
    {
        var client = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("login-bad");
        var registerAuth = await client.RegisterOkAsync("Acme", email, "correct-password");

        var response = await client.LoginAsync(email, "wrong-password");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        client.AuthorizeWith(registerAuth.AccessToken);
        var logs = await client.GetAuditLogsAsync();
        Assert.Contains(logs, l => l.EventType == "auth.login.failed");
    }

    [Fact]
    public async Task Refresh_WithValidToken_RotatesAndReturnsNewPair()
    {
        var client = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("refresh");
        var auth = await client.RegisterOkAsync("Acme", email);

        var refreshResponse = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(auth.RefreshToken));
        refreshResponse.EnsureSuccessStatusCode();
        var rotated = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(rotated);
        Assert.NotEqual(auth.RefreshToken, rotated!.RefreshToken);
        Assert.NotEqual(auth.AccessToken, rotated.AccessToken);
    }

    [Fact]
    public async Task Refresh_ReuseOfRevokedToken_RevokesWholeChainAndFailsAgain()
    {
        var client = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("reuse");
        var auth = await client.RegisterOkAsync("Acme", email);

        // First rotation succeeds and revokes the original token.
        var first = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(auth.RefreshToken));
        first.EnsureSuccessStatusCode();
        var rotated = (await first.Content.ReadFromJsonAsync<AuthResponse>())!;

        // Reusing the now-revoked original token is theft-like reuse: must fail...
        var reuse = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // ...and must also revoke the token that replaced it, breaking the whole chain.
        var afterReuse = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var client = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("logout");
        var auth = await client.RegisterOkAsync("Acme", email);

        var logout = await client.PostAsJsonAsync("/auth/logout", new LogoutRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refreshAfterLogout = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }
}
