using Hermes.Infrastructure.Adapters.Outbound.Persistence.Dapper;
using Xunit;

namespace Hermes.UnitTests.Dapper;

public sealed class MySqlConnectionFactoryTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenConnectionStringIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MySqlConnectionFactory(null!));
    }

    [Fact]
    public void Constructor_Initializes_WithValidConnectionString()
    {
        var factory = new MySqlConnectionFactory("Server=localhost;Database=test;Uid=root;Pwd=secret;");
        Assert.NotNull(factory);
    }
}
