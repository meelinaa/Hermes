namespace Hermes.Application.Options;

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
