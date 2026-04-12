namespace Hermes.Application.Models;

/// <summary>Outcome of a login attempt (no secrets). On success, <see cref="Email"/> and <see cref="Name"/> feed JWT claims.</summary>
public sealed record LoginResult(
    bool Success,
    string? ErrorMessage,
    int? UserId,
    string? Email = null,
    string? Name = null);
