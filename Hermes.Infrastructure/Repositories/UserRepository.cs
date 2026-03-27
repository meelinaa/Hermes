using Hermes.Domain.Entities;
using Hermes.Infrastructure.Data.Interfaces;
using Hermes.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/>.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly IHermesDbContext _db;

    /// <summary>
    /// Initializes a new instance of <see cref="UserRepository"/>.
    /// </summary>
    public UserRepository(IHermesDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<User?> GetUserByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim();
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CreateUserAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await _db.Users.AddAsync(user, ct).ConfigureAwait(false);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateUserAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        _db.Users.Update(user);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        _db.Users.Remove(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
