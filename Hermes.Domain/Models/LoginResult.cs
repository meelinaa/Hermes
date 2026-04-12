namespace Hermes.Application.Models;

/// <summary>
/// Outcome of a login attempt (no secrets in this object).
/// On success, <see cref="Email"/> and <see cref="Name"/> are copied into JWT claims when issuing the access token.
/// </summary>
public sealed record LoginResult(
    bool Success,
    string? ErrorMessage,
    int? UserId,
    string? Email = null,
    string? Name = null);
