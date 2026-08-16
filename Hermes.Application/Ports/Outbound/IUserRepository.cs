namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Composite outbound repository interface aggregating user CRUD operations (<see cref="IUserStore"/>),
/// authentication credential queries (<see cref="IUserAuthStore"/>), and email verification challenge state (<see cref="IUserVerificationStore"/>).
/// </summary>
public interface IUserRepository : IUserStore, IUserAuthStore, IUserVerificationStore
{
}
