namespace Hermes.Application.Models.Email;
/// <summary>
/// Describes a recipient address and optional display name.
/// </summary>
/// <param name="Address">E-mail address.</param>
/// <param name="DisplayName">Display name, or <c>null</c> if none.</param>
public sealed record EmailRecipient(string Address, string? DisplayName = null);