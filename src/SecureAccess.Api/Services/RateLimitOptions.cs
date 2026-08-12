namespace SecureAccess.Api.Services;

// Defaults are conservative-but-test-friendly; per-endpoint limits because register/login/
// refresh have very different legitimate call volumes (a real user refreshes far more often
// than they log in, and almost never registers more than once).
public class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public int LoginPermitLimit { get; set; } = 20;
    public int LoginWindowSeconds { get; set; } = 60;

    public int RegisterPermitLimit { get; set; } = 30;
    public int RegisterWindowSeconds { get; set; } = 60;

    public int RefreshPermitLimit { get; set; } = 30;
    public int RefreshWindowSeconds { get; set; } = 60;
}
