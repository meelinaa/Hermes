namespace Hermes.Application.DTOs.Login;

public sealed class LoginRequest
{
    public string NameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
