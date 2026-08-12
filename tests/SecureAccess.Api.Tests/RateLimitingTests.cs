using System.Net;
using System.Net.Http.Json;
using SecureAccess.Api.Features.Auth;
using SecureAccess.Api.Tests.Infrastructure;

namespace SecureAccess.Api.Tests;

[Collection("RateLimit")]
public class RateLimitingTests
{
    private readonly RateLimitTestApiFactory _factory;

    public RateLimitingTests(RateLimitTestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ExceedingRateLimit_Returns429()
    {
        var client = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("ratelimit-login");
        await client.RegisterOkAsync("RateLimit Org", email);

        // Configured limit for this factory is 3/60s; wrong password so we don't burn through
        // successful logins, just endpoint hits.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await client.LoginAsync(email, "wrong-password");
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        // Everything before the limit kicked in should be a normal auth failure, not a server error.
        Assert.All(statuses.TakeWhile(s => s != HttpStatusCode.TooManyRequests), s => Assert.Equal(HttpStatusCode.Unauthorized, s));
    }

    [Fact]
    public async Task Register_ExceedingRateLimit_Returns429()
    {
        var client = _factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await client.RegisterAsync($"Org {Guid.NewGuid()}", ApiClientExtensions.UniqueEmail("ratelimit-register"));
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Refresh_ExceedingRateLimit_Returns429()
    {
        var client = _factory.CreateClient();
        var email = ApiClientExtensions.UniqueEmail("ratelimit-refresh");
        var auth = await client.RegisterOkAsync("RateLimit Org", email);

        // The token becomes invalid after the first successful rotation, but the rate limiter
        // still counts every hit against the endpoint regardless of what the handler decides.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(auth.RefreshToken));
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
