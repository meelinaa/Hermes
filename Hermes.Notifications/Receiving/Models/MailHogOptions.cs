namespace Hermes.Notifications.Receiving.Models;

public sealed class MailHogOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public bool SendSchedulerTestMailEachMinute { get; set; }
}
