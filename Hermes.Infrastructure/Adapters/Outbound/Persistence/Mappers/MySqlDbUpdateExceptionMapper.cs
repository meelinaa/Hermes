using FluentResults;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Mappers;

/// <summary>
/// Maps common MySQL error numbers from EF <see cref="DbUpdateException"/> into domain errors.
/// </summary>
internal static class MySqlDbUpdateExceptionMapper
{
    /// <summary>
    /// Transforms known MySQL exception codes into domain errors; otherwise returns null.
    /// </summary>
    /// <param name="ex">The DbUpdateException to transform.</param>
    /// <returns>The mapped error or null if not recognized.</returns>
    public static IError? MapToError(DbUpdateException ex)
    {
        if (ex.InnerException is MySqlException mysql)
        {
            if (mysql.Number == 1452)
                return new Error("A related record was not found (foreign key constraint).");
            if (mysql.Number == 1062)
                return new Error("A unique constraint was violated.");
        }

        return null;
    }
}
