namespace Hermes.Application.DTOs.Login;

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
