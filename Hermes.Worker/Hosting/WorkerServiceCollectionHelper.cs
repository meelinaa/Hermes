using Hermes.Application.Models.Email;
using Hermes.Application.Options;
using Hermes.Notifications.Receiving.Models;
using Microsoft.Extensions.Options;

namespace Hermes.Worker.Hosting;

public class WorkerServiceCollectionHelper
{
    /// <summary>
    /// Reads <c>.env</c> (line <c>NEWSDATA.IO: &lt;apiKey&gt;</c>) from several search folders so the key reaches <see cref="NewsDataIoOptions.ApiKey"/> / <see cref="IOptions{T}"/>.
    /// </summary>
    internal static string? TryReadNewsDataIoKeyFromDotEnv(string contentRootPath)
    {
        const string linePrefix = "NEWSDATA.IO:";
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        foreach (var dir in new[] { contentRootPath, AppContext.BaseDirectory, Directory.GetCurrentDirectory(), exeDir })
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;
            var path = Path.Combine(dir, ".env");
            if (!File.Exists(path))
                continue;

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                if (!line.StartsWith(linePrefix, StringComparison.Ordinal))
                    continue;

                var value = line[linePrefix.Length..].Trim();
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                return value;
            }
        }

        return null;
    }

    internal static EmailSettings BindEmailSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection("Email");
        var host = section["Host"]
            ?? throw new InvalidOperationException("Configure Email:Host (SMTP server).");
        var from = section["DefaultFromAddress"]
            ?? throw new InvalidOperationException("Configure Email:DefaultFromAddress.");
        var replyTo = section["DefaultReplyToAddress"] ?? from;
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

    /// <summary>Logs SMTP target and MailHog web UI </summary>
    public static void LogMailHogDevHints(IHost host)
    {
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hermes.Worker");
        var smtp = host.Services.GetRequiredService<EmailSettings>();
        logger.LogInformation(
            "SMTP: {Host}:{Port} (SSL={Ssl}), From={From} — für lokales MailHog typisch Port 1025.",
            smtp.Host,
            smtp.Port,
            smtp.EnableSsl,
            smtp.DefaultFromAddress);

        var mailHog = host.Services.GetService<IOptions<MailHogSettings>>()?.Value;
        if (mailHog is not null && !string.IsNullOrWhiteSpace(mailHog.BaseUrl))
            logger.LogInformation("MailHog-Web-UI: {BaseUrl}", mailHog.BaseUrl.TrimEnd('/'));
    }
}
