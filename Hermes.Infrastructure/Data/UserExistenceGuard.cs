using Hermes.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Data;

/// <summary>Shared FK precondition checks against <see cref="HermesDbContext.Users"/>.</summary>
internal static class UserExistenceGuard
{
    public static async Task EnsureExistsAsync(HermesDbContext db, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
            throw new UserNotFoundException($"No user with id {userId} exists.");
        bool exists = await db.Users.AsNoTracking()
            .AnyAsync(userEntity => userEntity.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new UserNotFoundException($"No user with id {userId} exists.");
    }
}
