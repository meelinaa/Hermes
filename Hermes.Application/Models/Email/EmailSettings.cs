namespace Hermes.Application.Models.Email;

/// <summary>
/// SMTP and default envelope settings used when sending mail.
/// </summary>
/// <param name="Host">SMTP server host name.</param>
/// <param name="Port">SMTP server port.</param>
/// <param name="EnableSsl">Whether to use SSL/TLS for the SMTP connection.</param>
/// <param name="Username">SMTP authentication user name, or <c>null</c> if not used.</param>
/// <param name="Password">SMTP authentication password, or <c>null</c> if not used.</param>
/// <param name="DefaultFromAddress">Default sender e-mail address.</param>
/// <param name="DefaultFromName">Default sender display name.</param>
/// <param name="DefaultReplyToAddress">Default Reply-To address.</param>
/// <param name="DefaultReplyToName">Default Reply-To display name.</param>
/// <param name="XMailer">Value for the <c>X-Mailer</c> header (identifies the sending software).</param>
public sealed record EmailSettings(
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
