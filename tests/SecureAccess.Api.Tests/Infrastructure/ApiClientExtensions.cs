using System.Net.Http.Headers;
using System.Net.Http.Json;
using SecureAccess.Api.Features.AuditLogs;
using SecureAccess.Api.Features.Auth;
using SecureAccess.Api.Features.Roles;
using SecureAccess.Api.Features.Users;

namespace SecureAccess.Api.Tests.Infrastructure;

public static class ApiClientExtensions
{
    public static async Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string orgName, string email, string password = "P@ssw0rd123!")
        => await client.PostAsJsonAsync("/auth/register", new RegisterRequest(orgName, email, password));

    public static async Task<AuthResponse> RegisterOkAsync(this HttpClient client, string orgName, string email, string password = "P@ssw0rd123!")
    {
        var response = await client.RegisterAsync(orgName, email, password);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password)
        => await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));

    public static void AuthorizeWith(this HttpClient client, string accessToken)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    public static async Task<List<RoleResponse>> GetRolesAsync(this HttpClient client)
        => (await client.GetFromJsonAsync<List<RoleResponse>>("/roles"))!;

    public static async Task<List<AuditLogResponse>> GetAuditLogsAsync(this HttpClient client)
        => (await client.GetFromJsonAsync<List<AuditLogResponse>>("/audit-logs"))!;

    public static async Task<List<UserResponse>> GetUsersAsync(this HttpClient client)
        => (await client.GetFromJsonAsync<List<UserResponse>>("/users"))!;

    public static async Task<HttpResponseMessage> CreateUserAsync(this HttpClient client, string email, string password, Guid? roleId)
        => await client.PostAsJsonAsync("/users", new CreateUserRequest(email, password, roleId));

    // Unique per-call so parallel/repeated test runs never collide on the unique Email index.
    public static string UniqueEmail(string label) => $"{label}-{Guid.NewGuid():N}@test.local";
}
