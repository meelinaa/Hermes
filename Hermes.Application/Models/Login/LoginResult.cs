namespace Hermes.Application.Models.Login;

public sealed record LoginResult(
    bool Success,
    string? ErrorMessage,
    int? UserId,
    string? Email = null,
    string? Name = null);
