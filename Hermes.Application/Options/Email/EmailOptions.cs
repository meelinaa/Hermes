namespace Hermes.Application.Options.Email;

/// <summary>
/// Configuration options for outbound email SMTP delivery.
/// </summary>
public sealed record EmailOptions(
    string Host,
    int Port,
    bool EnableSsl,
    string? Username,
    string? Password,
    string DefaultFromAddress,
    string DefaultFromName,
    string DefaultReplyToAddress,
    string DefaultReplyToName,
    string XMailer);
