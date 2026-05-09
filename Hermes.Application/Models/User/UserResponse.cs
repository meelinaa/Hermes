namespace Hermes.Application.Models.User;

/// <summary>Public user projection returned by user GET/register/update and after successful e-mail verification.</summary>
public sealed record UserResponse
{
    public int UserId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public bool IsEmailVerified { get; init; }
}
