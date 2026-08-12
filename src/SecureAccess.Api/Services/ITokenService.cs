using SecureAccess.Api.Domain;

namespace SecureAccess.Api.Services;

public record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

public interface ITokenService
{
    string CreateAccessToken(User user, IEnumerable<string> roleNames);

    // Generates a new refresh token, stores its hash, and returns the raw token to hand to the client.
    Task<TokenPair> IssueTokenPairAsync(User user, IEnumerable<string> roleNames, CancellationToken ct = default);

    // Validates the raw refresh token, revokes it, issues a replacement (rotation).
    // Returns null if the token is invalid/expired. Throws if reuse of an already-revoked token is detected.
    Task<TokenPair?> RotateRefreshTokenAsync(string rawRefreshToken, CancellationToken ct = default);

    Task RevokeRefreshTokenAsync(string rawRefreshToken, CancellationToken ct = default);
}
