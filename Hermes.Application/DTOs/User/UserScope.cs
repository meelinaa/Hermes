namespace Hermes.Application.DTOs.User;

/// <summary>
/// Internal DTO representing user summary scope.
/// </summary>
public class UserScope
{
    /// <summary>
    /// Gets or sets the name of the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email of the user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique user identifier.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user's email is verified.
    /// </summary>
    public bool IsEmailVerified { get; set; }  
}
