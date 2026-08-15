namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>
/// Data transfer object used to update a user's name, email, or password.
/// </summary>
public sealed record UserProfileUpdateRequestDto
{
    /// <summary>The unique ID of the user to update.</summary>
    public int Id { get; init; }

    /// <summary>The updated display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The updated email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>The current password plain text for validation when updating password.</summary>
    public string? CurrentPassword { get; init; }

    /// <summary>The new password plain text.</summary>
    public string? NewPassword { get; init; }
}
