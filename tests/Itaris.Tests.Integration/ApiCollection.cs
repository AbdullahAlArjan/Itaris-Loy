namespace Itaris.Tests.Integration;

/// <summary>
/// All integration tests share ONE API host + PostgreSQL container.
///
/// This matters for correctness, not just speed: <see cref="ApiFixture"/> publishes the
/// container's connection string through a process-wide environment variable, so multiple
/// fixtures running in parallel would overwrite each other and let a host talk to another
/// fixture's (possibly disposed) container. One collection fixture makes that deterministic
/// and serializes the classes.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
