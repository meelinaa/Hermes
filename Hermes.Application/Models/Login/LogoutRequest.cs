namespace Hermes.Application.Models.Login;

public sealed class LogoutRequest
{
    public string? RefreshToken { get; set; }
}
