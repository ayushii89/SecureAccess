using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SecureAccess.Api.Data;
using SecureAccess.Api.Domain;

namespace SecureAccess.Api.Services;

public class TokenService : ITokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _options;

    public TokenService(AppDbContext db, IOptions<JwtOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public string CreateAccessToken(User user, IEnumerable<string> roleNames)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("org_id", user.OrganizationId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roleNames.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Convert.FromBase64String(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<TokenPair> IssueTokenPairAsync(User user, IEnumerable<string> roleNames, CancellationToken ct = default)
    {
        var roles = roleNames.ToList();
        var accessToken = CreateAccessToken(user, roles);
        var (rawRefreshToken, expiresAt) = await CreateAndStoreRefreshTokenAsync(user.Id, ct);
        return new TokenPair(accessToken, rawRefreshToken, expiresAt);
    }

    public async Task<TokenPair?> RotateRefreshTokenAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(rawRefreshToken);
        var existing = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (existing is null)
        {
            return null;
        }

        if (existing.RevokedAt is not null)
        {
            // Reuse of a revoked token — likely theft. Revoke every active token for this user.
            var activeTokens = await _db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(rt => rt.UserId == existing.UserId && rt.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var t in activeTokens)
            {
                t.RevokedAt = DateTimeOffset.UtcNow;
            }
            await _db.SaveChangesAsync(ct);
            return null;
        }

        if (!existing.IsActive)
        {
            return null;
        }

        var user = existing.User!;
        var roles = await _db.UserRoles
            .IgnoreQueryFilters()
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role!.Name)
            .ToListAsync(ct);

        var (rawNewToken, expiresAt) = await CreateAndStoreRefreshTokenAsync(user.Id, ct);
        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.ReplacedByTokenHash = HashToken(rawNewToken);
        await _db.SaveChangesAsync(ct);

        var accessToken = CreateAccessToken(user, roles);
        return new TokenPair(accessToken, rawNewToken, expiresAt);
    }

    public async Task RevokeRefreshTokenAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(rawRefreshToken);
        var existing = await _db.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);
        if (existing is not null && existing.RevokedAt is null)
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<(string RawToken, DateTimeOffset ExpiresAt)> CreateAndStoreRefreshTokenAsync(Guid userId, CancellationToken ct)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = expiresAt,
        });
        await _db.SaveChangesAsync(ct);

        return (rawToken, expiresAt);
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
