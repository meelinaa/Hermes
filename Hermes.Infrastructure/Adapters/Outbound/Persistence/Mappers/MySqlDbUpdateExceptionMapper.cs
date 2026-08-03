using Hermes.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Mappers;

/// <summary>
/// Maps common MySQL error numbers from EF <see cref="DbUpdateException"/> into domain failures.
/// </summary>
internal static class MySqlDbUpdateExceptionMapper
{
    /// <summary>
    /// Transforms known MySQL exception codes into domain exceptions; otherwise rethrows <paramref name="ex"/>.
    /// </summary>
    /// <param name="ex">The DbUpdateException to transform.</param>
    /// <returns>The mapped domain exception or the original exception.</returns>
    public static Exception Transform(DbUpdateException ex)
    {
        if (ex.InnerException is MySqlException mysql)
        {
            if (mysql.Number == 1452)
                return new UserNotFoundException("A related record was not found (foreign key constraint).");
            if (mysql.Number == 1062)
                return new EmailAlreadyExistsException("A unique constraint was violated.");
        }

        return ex;
    }
}
