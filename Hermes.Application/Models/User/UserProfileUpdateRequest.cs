namespace Hermes.Application.Models;

/// <summary>Body for <c>PUT /api/v1/users</c> (profile update). JSON camelCase.</summary>
public sealed class UserProfileUpdateRequest
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    /// <summary>Plain new password; omit or empty to keep the current password.</summary>
    public string? NewPassword { get; set; }

    /// <summary>Required when <see cref="NewPassword"/> is set: plain current password.</summary>
    public string? CurrentPassword { get; set; }
}
