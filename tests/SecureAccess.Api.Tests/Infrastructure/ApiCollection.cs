namespace SecureAccess.Api.Tests.Infrastructure;

// All test classes share one ApiFactory (one DB reset, one migration) and run sequentially —
// they share the same Postgres test database, so concurrent resets would race.
[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;
