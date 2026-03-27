using Hermes.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Data.Interfaces
{
    public interface IHermesDbContext
    {
        DbSet<User> Users { get; }
        DbSet<News> News { get; }
        DbSet<NotificationLog> NotificationLogs { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
