using Hermes.Domain.Exceptions;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Validators;

/// <summary>
/// Shared FK precondition validator for checking user existence against <see cref="HermesDbContext.Users"/>.
/// </summary>
internal static class UserExistenceValidator
{
    /// <summary>
    /// Validates that a user with the specified ID exists in the database.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="userId">The user ID to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous validation.</returns>
    /// <exception cref="UserNotFoundException">Thrown if the user does not exist or userId is invalid.</exception>
    public static async Task EnsureExistsAsync(HermesDbContext db, UserId userId, CancellationToken cancellationToken)
    {
        if (userId.Value <= 0)
            throw new UserNotFoundException($"No user with id {userId.Value} exists.");
        bool exists = await db.Users.AsNoTracking()
            .AnyAsync(userEntity => userEntity.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new UserNotFoundException($"No user with id {userId.Value} exists.");
    }
}
