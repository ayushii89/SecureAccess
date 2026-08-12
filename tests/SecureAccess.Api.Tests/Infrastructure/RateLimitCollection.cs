namespace SecureAccess.Api.Tests.Infrastructure;

// Deliberately not the "Api" collection: this factory targets its own database
// (secureaccess_test_ratelimit) with its own stricter rate-limit config, so it needs its
// own lifecycle rather than sharing the shared factory's reset/migrate cycle.
[CollectionDefinition("RateLimit")]
public class RateLimitCollection : ICollectionFixture<RateLimitTestApiFactory>;
