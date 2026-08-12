namespace SecureAccess.Api.Services;

// Single-use, short-lived handoff so the OAuth redirect back to the SPA never puts the real
// JWT/refresh token in a URL (browser history, referrer headers, server logs). Singleton +
// in-memory is fine for this single-instance deployment; a multi-instance one would need a
// distributed store instead.
public interface IOAuthCodeStore
{
    Guid Store(TokenPair tokens);

    // Returns null if the code is unknown, expired, or already consumed.
    TokenPair? TryConsume(Guid code);
}
