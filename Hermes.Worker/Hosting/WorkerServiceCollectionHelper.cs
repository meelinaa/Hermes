using Hermes.Application.Models.Email;
using Hermes.Application.Options;
using Hermes.Notifications.Receiving.Models;
using Microsoft.Extensions.Options;

namespace Hermes.Worker.Hosting;

/// <summary>Helper methods for worker configuration binding, environment discovery, and startup diagnostics.</summary>
public class WorkerServiceCollectionHelper
{
    /// <summary>
    /// Reads the NewsData.io API key only from a <c>.env</c> file (not from <c>appsettings</c>).
    /// Supported lines: <c>NEWSDATA.IO: &lt;apiKey&gt;</c>, <c>NewsDataIo__ApiKey=&lt;apiKey&gt;</c>, or <c>NEWSDATA_IO_API_KEY=&lt;apiKey&gt;</c>.
    /// Searches content root, base directory, current directory, executable directory, and walks up from each to find <c>.env</c>.
    /// </summary>
    internal static string? TryReadNewsDataIoApiKeyFromEnvFile(string contentRootPath)
    {
        foreach (string envPath in EnumerateEnvFilePaths(contentRootPath))
        {
            string? key = TryParseNewsDataIoKeyFromEnvFile(envPath);
            if (!string.IsNullOrWhiteSpace(key))
                return key.Trim();
        }

        return null;
    }

    /// <summary>Enumerates candidate <c>.env</c> file paths by traversing known start directories and parent folders.</summary>
    private static IEnumerable<string> EnumerateEnvFilePaths(string contentRootPath)
    {
        string? exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        string?[] starts = [contentRootPath, AppContext.BaseDirectory, Directory.GetCurrentDirectory(), exeDir];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string? start in starts)
        {
            if (string.IsNullOrWhiteSpace(start))
                continue;
            string? dir = Path.GetFullPath(start);
            for (int depth = 0; depth < 8 && !string.IsNullOrEmpty(dir); depth++)
            {
                string candidate = Path.Combine(dir, ".env");
                if (File.Exists(candidate) && seen.Add(candidate))
                    yield return candidate;
                dir = Directory.GetParent(dir)?.FullName;
            }
        }
    }

    /// <summary>Parses a NewsData.io API key from one <c>.env</c> file using supported key formats.</summary>
    private static string? TryParseNewsDataIoKeyFromEnvFile(string envFilePath)
    {
        const string COLON_PREFIX = "NEWSDATA.IO:";
        foreach (string rawLine in File.ReadLines(envFilePath))
        {
            string line = rawLine.Trim().TrimStart('\uFEFF');
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith(COLON_PREFIX, StringComparison.Ordinal))
            {
                string parsedValue = line[COLON_PREFIX.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(parsedValue))
                    return StripOptionalQuotes(parsedValue);
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            string keyName = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;
            if (keyName.Equals("NewsDataIo__ApiKey", StringComparison.OrdinalIgnoreCase) ||
                keyName.Equals("NEWSDATA_IO_API_KEY", StringComparison.OrdinalIgnoreCase))
                return StripOptionalQuotes(value);
        }

        return null;
    }

    /// <summary>Removes matching wrapping single or double quotes from a value.</summary>
    private static string StripOptionalQuotes(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1].Trim();
        return value;
    }

    /// <summary>Binds SMTP mail settings from configuration and validates required fields.</summary>
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

    /// <summary>Logs SMTP target and MailHog web UI </summary>
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
