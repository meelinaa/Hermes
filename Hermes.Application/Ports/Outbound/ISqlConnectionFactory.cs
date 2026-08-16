using System.Data;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Defines an asynchronous database connection factory for lightweight, read-only SQL queries (e.g. via Dapper).
/// Enables high-performance CQRS query paths decoupled from EF Core entity tracking overhead.
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// Creates and asynchronously opens a new database connection for executing read-only SQL queries.
    /// The caller is responsible for disposing the returned connection.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the connection to open.</param>
    /// <returns>An open <see cref="IDbConnection"/> instance ready for command execution.</returns>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
