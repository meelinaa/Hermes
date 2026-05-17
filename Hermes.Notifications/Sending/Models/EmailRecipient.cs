namespace Hermes.Notifications.Sending.Models;
public sealed record EmailRecipient(string Address, string? DisplayName = null);
