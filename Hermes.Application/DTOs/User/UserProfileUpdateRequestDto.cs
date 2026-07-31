namespace Hermes.Application.DTOs.User;

public sealed class UserProfileUpdateRequestDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? NewPassword { get; set; }

    public string? CurrentPassword { get; set; }
}
