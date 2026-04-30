namespace Hermes.IntegrationTests.Infrastructure;

/// <summary>
/// Names the Xunit collection that owns the shared <see cref="MySqlApiFixture"/> (single MySQL container + API factory per assembly run group).
/// </summary>
[CollectionDefinition(nameof(HermesIntegrationCollection))]
public sealed class HermesIntegrationCollection : ICollectionFixture<MySqlApiFixture>
{
}
