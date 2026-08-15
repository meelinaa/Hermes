using System.Data;
using Hermes.Application.Ports.Outbound;
using MySqlConnector;

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Dapper;

/// <summary>
/// MySQL-specific implementation of <see cref="ISqlConnectionFactory"/> providing open connections for Dapper queries.
/// </summary>
public sealed class MySqlConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    /// <summary>
    /// Instantiates and opens a new <see cref="MySqlConnection"/> asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the database connection to open.</param>
    /// <returns>An open database connection ready for query execution.</returns>
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
