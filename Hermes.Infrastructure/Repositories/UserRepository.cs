using Hermes.Domain.Entities;
using Hermes.Domain.Interfaces.DBContext;
using Hermes.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="UserRepository"/>.
/// </remarks>
public sealed class UserRepository(IHermesDbContext db) : IUserRepository
{

    /// <inheritdoc />
    public async Task<User?> GetUserByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CreateUserAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await db.Users.AddAsync(user, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateUserAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        db.Users.Update(user);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        db.Users.Remove(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
