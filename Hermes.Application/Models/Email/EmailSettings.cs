namespace Hermes.Application.Models.Email;

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
