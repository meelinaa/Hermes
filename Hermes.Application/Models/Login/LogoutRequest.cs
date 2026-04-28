namespace Hermes.Application.Models.Login;

/// <summary>
/// Optional body for <c>POST /auth/logout</c> (requires a valid JWT).
/// If <see cref="RefreshToken"/> is null or empty, every active refresh token for that user is revoked.
/// If set, only that refresh session is revoked (must belong to the JWT user).
/// </summary>
public sealed class LogoutRequest
{
    public string? RefreshToken { get; set; }
}
