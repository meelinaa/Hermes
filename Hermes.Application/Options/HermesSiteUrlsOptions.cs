namespace Hermes.Application.Options;

public sealed class HermesSiteUrlsOptions
{
    public const string SECTION_NAME = "Hermes";

    public string PublicBaseUrl { get; set; } = "https://hermes.de";

    public string SupportEmail { get; set; } = "support@hermes.de";
}
