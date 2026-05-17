namespace Hermes.Notifications.Receiving.Models;

public sealed record EmailResult(
    string Id,
    string From,
    string To,
    string Subject,
    string Body,
    DateTimeOffset ReceivedAt);
