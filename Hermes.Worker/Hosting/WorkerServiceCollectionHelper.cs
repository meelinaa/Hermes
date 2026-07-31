using Hermes.Application.DTOs.Email;
using Hermes.Application.Options;
using Hermes.Notifications.Receiving.Models;
using Microsoft.Extensions.Options;

namespace Hermes.Worker.Hosting;

public class WorkerServiceCollectionHelper
{
    internal static EmailSettings BindEmailSettings(IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection("Email");
        string host = section["Host"]
            ?? throw new InvalidOperationException("Configure Email:Host (SMTP server).");
        string from = section["DefaultFromAddress"]
            ?? throw new InvalidOperationException("Configure Email:DefaultFromAddress.");
        string replyTo = section["DefaultReplyToAddress"] ?? from;
        return new EmailSettings(
            host,
            section.GetValue("Port", 25),
            section.GetValue("EnableSsl", false),
            string.IsNullOrWhiteSpace(section["Username"]) ? null : section["Username"],
            string.IsNullOrWhiteSpace(section["Password"]) ? null : section["Password"],
            from,
            section["DefaultFromName"] ?? "Hermes",
            replyTo,
            section["DefaultReplyToName"] ?? section["DefaultFromName"] ?? "Hermes",
            section["XMailer"] ?? "Hermes.Worker");
    }

    public static void LogMailHogDevHints(IHost host)
    {
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hermes.Worker");
        EmailSettings smtp = host.Services.GetRequiredService<EmailSettings>();
        logger.LogInformation(
            "SMTP: {Host}:{Port} (SSL={Ssl}), From={From} — für lokales MailHog typisch Port 1025.",
            smtp.Host,
            smtp.Port,
            smtp.EnableSsl,
            smtp.DefaultFromAddress);

        MailHogSettings? mailHog = host.Services.GetService<IOptions<MailHogSettings>>()?.Value;
        if (mailHog is not null && !string.IsNullOrWhiteSpace(mailHog.BaseUrl))
            logger.LogInformation("MailHog-Web-UI: {BaseUrl}", mailHog.BaseUrl.TrimEnd('/'));
    }
}
