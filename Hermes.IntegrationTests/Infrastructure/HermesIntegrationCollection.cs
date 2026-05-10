namespace Hermes.IntegrationTests.Infrastructure;

[CollectionDefinition(nameof(HermesIntegrationCollection))]
public sealed class HermesIntegrationCollection : ICollectionFixture<MySqlApiFixture>
{
}
