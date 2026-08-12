using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace SecureAccess.Api.Tests.Infrastructure;

// A separate factory (own DB, own tighter rate-limit config) so these tests can trip the
// limiter in a handful of requests without stealing budget from — or racing the DB reset
// against — the shared ApiFactory used by every other test class.
public class RateLimitTestApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=secureaccess_test_ratelimit;Username=secureaccess;Password=secureaccess_dev",
                ["RateLimiting:LoginPermitLimit"] = "3",
                ["RateLimiting:LoginWindowSeconds"] = "60",
                ["RateLimiting:RegisterPermitLimit"] = "3",
                ["RateLimiting:RegisterWindowSeconds"] = "60",
                ["RateLimiting:RefreshPermitLimit"] = "3",
                ["RateLimiting:RefreshWindowSeconds"] = "60",
            });
        });
    }
}
