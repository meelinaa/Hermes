namespace Hermes.Application.DTOs.User;

/// <summary>
/// Request payload for registering a new user.
/// </summary>
public class RegisterUserRequest
{
    /// <summary>
    /// Gets or sets the name of the user.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the email address of the user.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Gets or sets the password of the user.
    /// </summary>
    public required string Password { get; set; }
}
