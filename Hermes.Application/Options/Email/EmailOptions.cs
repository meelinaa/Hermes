using System.ComponentModel.DataAnnotations;

namespace Hermes.Application.Options.Email;

/// <summary>
/// Configuration options for outbound email SMTP delivery.
/// </summary>
public sealed class EmailOptions
{
    public const string SECTION_NAME = "Email";

    [Required(ErrorMessage = "Host is required.")]
    public string Host { get; set; } = null!;

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    public int Port { get; set; }

    public bool EnableSsl { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required]
    [EmailAddress]
    public string DefaultFromAddress { get; set; } = null!;

    [Required]
    public string DefaultFromName { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string DefaultReplyToAddress { get; set; } = null!;

    [Required]
    public string DefaultReplyToName { get; set; } = null!;

    [Required]
    public string XMailer { get; set; } = null!;
}
