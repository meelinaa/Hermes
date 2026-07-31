namespace Hermes.Application.DTOs.Email;
public sealed record EmailRecipient(string Address, string? DisplayName = null);
