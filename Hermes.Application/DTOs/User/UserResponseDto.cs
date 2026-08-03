namespace Hermes.Application.DTOs.User;

public sealed record UserResponseDto
{
    public int UserId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public bool IsEmailVerified { get; init; }
}
