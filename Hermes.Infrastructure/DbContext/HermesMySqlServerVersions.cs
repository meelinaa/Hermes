using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Data;

/// <summary>
/// Pomelo server-version tokens without probing the server during EF options registration (AutoDetect opens a TCP connection).
/// Used where an early handshake is undesirable (e.g. <c>Testing</c>, integration migrator).
/// </summary>
/// <remarks>Aligned with Docker <c>mysql:8.4</c> in <c>Hermes.IntegrationTests</c> and sufficient for Pomelo generation against MySQL 8.x.</remarks>
public static class HermesMySqlServerVersions
{
    /// <summary>Capability version matching MySQL 8.4 (Testcontainers integration image); no database round-trip.</summary>
    public static ServerVersion PinnedMysql84 { get; } = ServerVersion.Parse("8.4.0-mysql");
}
