namespace Hermes.Application.DTOs.Email;

public sealed record EmailRecipientDto(string Address, string? DisplayName = null);
