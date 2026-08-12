using System.Collections.Concurrent;

namespace SecureAccess.Api.Services;

public class OAuthCodeStore : IOAuthCodeStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<Guid, (TokenPair Tokens, DateTimeOffset Expires)> _entries = new();

    public Guid Store(TokenPair tokens)
    {
        SweepExpired();
        var code = Guid.NewGuid();
        _entries[code] = (tokens, DateTimeOffset.UtcNow.Add(Ttl));
        return code;
    }

    public TokenPair? TryConsume(Guid code)
    {
        if (!_entries.TryRemove(code, out var entry))
        {
            return null;
        }
        return entry.Expires > DateTimeOffset.UtcNow ? entry.Tokens : null;
    }

    private void SweepExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, value) in _entries)
        {
            if (value.Expires <= now)
            {
                _entries.TryRemove(key, out _);
            }
        }
    }
}
