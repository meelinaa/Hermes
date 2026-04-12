namespace Hermes.Application.Models;

/// <summary>Outcome of a login attempt (no secrets).</summary>
public sealed record LoginResult(bool Success, string? ErrorMessage, int? UserId);
