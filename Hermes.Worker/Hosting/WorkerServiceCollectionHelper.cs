using Hermes.Application.Models.Email;
using Hermes.Application.Options;
using Hermes.Notifications.Receiving.Models;
using Microsoft.Extensions.Options;

namespace Hermes.Worker.Hosting;

public class WorkerServiceCollectionHelper
{
    /// <summary>
    /// Reads the NewsData.io API key only from a <c>.env</c> file (not from <c>appsettings</c>).
    /// Supported lines: <c>NEWSDATA.IO: &lt;apiKey&gt;</c>, <c>NewsDataIo__ApiKey=&lt;apiKey&gt;</c>, or <c>NEWSDATA_IO_API_KEY=&lt;apiKey&gt;</c>.
    /// Searches content root, base directory, current directory, executable directory, and walks up from each to find <c>.env</c>.
    /// </summary>
    internal static string? TryReadNewsDataIoApiKeyFromEnvFile(string contentRootPath)
    {
        foreach (var envPath in EnumerateEnvFilePaths(contentRootPath))
        {
            var key = TryParseNewsDataIoKeyFromEnvFile(envPath);
            if (!string.IsNullOrWhiteSpace(key))
                return key.Trim();
        }

        return null;
    }

    private static IEnumerable<string> EnumerateEnvFilePaths(string contentRootPath)
    {
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        var starts = new[] { contentRootPath, AppContext.BaseDirectory, Directory.GetCurrentDirectory(), exeDir };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in starts)
        {
            if (string.IsNullOrWhiteSpace(start))
                continue;
            string? dir = Path.GetFullPath(start);
            for (var depth = 0; depth < 8 && !string.IsNullOrEmpty(dir); depth++)
            {
                var candidate = Path.Combine(dir, ".env");
                if (File.Exists(candidate) && seen.Add(candidate))
                    yield return candidate;
                dir = Directory.GetParent(dir)?.FullName;
            }
        }
    }

    private static string? TryParseNewsDataIoKeyFromEnvFile(string envFilePath)
    {
        const string colonPrefix = "NEWSDATA.IO:";
        foreach (var rawLine in File.ReadLines(envFilePath))
        {
            var line = rawLine.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith(colonPrefix, StringComparison.Ordinal))
            {
                var v = line[colonPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(v))
                    return StripOptionalQuotes(v);
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            var keyName = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;
            if (keyName.Equals("NewsDataIo__ApiKey", StringComparison.OrdinalIgnoreCase) ||
                keyName.Equals("NEWSDATA_IO_API_KEY", StringComparison.OrdinalIgnoreCase))
                return StripOptionalQuotes(value);
        }

        return null;
    }

    private static string StripOptionalQuotes(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1].Trim();
        return value;
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
