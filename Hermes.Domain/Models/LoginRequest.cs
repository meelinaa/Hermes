namespace Hermes.Application.Models;

/// <summary>Credentials for login: either display name or email in <see cref="NameOrEmail"/>.</summary>
public sealed class LoginRequest
{
    public string NameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
