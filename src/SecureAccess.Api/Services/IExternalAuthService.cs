namespace SecureAccess.Api.Services;

public interface IExternalAuthService
{
    // Looks up a user by email; creates a new Organization + Admin user on first sign-in
    // (mirrors AuthController.Register), otherwise treats it as a login. Either way, issues
    // a token pair. `provider` is recorded in the audit log metadata only.
    Task<TokenPair> CompleteLoginAsync(string email, string provider, CancellationToken ct = default);
}
