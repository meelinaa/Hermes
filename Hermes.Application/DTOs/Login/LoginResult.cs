namespace Hermes.Application.DTOs.Login;

public sealed record LoginResult(
    bool Success,
    string? ErrorMessage,
    int? UserId,
    string? Email = null,
    string? Name = null);
