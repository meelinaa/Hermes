namespace Hermes.Notifications.Receiving.Models;

public sealed record EmailResultDto(
    string Id,
    string From,
    string To,
    string Subject,
    string Body,
    DateTimeOffset ReceivedAt);
