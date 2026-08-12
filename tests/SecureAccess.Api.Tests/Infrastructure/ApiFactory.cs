using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecureAccess.Api.Data;

namespace SecureAccess.Api.Tests.Infrastructure;

// Boots the real API pipeline (auth, RBAC, tenant filters and all) against a dedicated
// `secureaccess_test` Postgres database, so these are true integration tests, not mocks.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=secureaccess_test;Username=secureaccess;Password=secureaccess_dev",
                ["Jwt:SigningKey"] = "dGVzdC1zaWduaW5nLWtleS1mb3ItaW50ZWdyYXRpb24tdGVzdHMtb25seQ==",
                ["Jwt:Issuer"] = "SecureAccess.Tests",
                ["Jwt:Audience"] = "SecureAccess.Tests.Clients",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Force host creation now (rather than on first client request) so migrations run
        // before any test issues a request.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await SeedData.EnsurePermissionCatalogAsync(db);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
}
