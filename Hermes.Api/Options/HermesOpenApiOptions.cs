using System.ComponentModel.DataAnnotations;

namespace Hermes.Api.Options;

/// <summary>
/// Options for configuring OpenAPI documentation, endpoint routing, and security key settings.
/// </summary>
public sealed class HermesOpenApiOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SECTION_NAME = "OpenApi";

    /// <summary>
    /// OpenAPI document identifier name (defaults to "v1").
    /// </summary>
    [Required]
    public string DocumentName { get; set; } = "v1";

    /// <summary>
    /// Route pattern template for exposing the OpenAPI JSON document.
    /// </summary>
    [Required]
    public string RoutePattern { get; set; } = "openapi/{documentName}.json";

    /// <summary>
    /// Enables OpenAPI documentation endpoints in Production environment when set to true.
    /// </summary>
    public bool MapInProduction { get; set; }

    /// <summary>
    /// API secret key required to access production documentation endpoints.
    /// </summary>
    public string DocumentationApiKey { get; set; } = "";

    /// <summary>
    /// HTTP header name used for transmitting the documentation access key.
    /// </summary>
    [Required]
    public string DocumentationApiKeyHeader { get; set; } = "X-Hermes-Documentation-Key";

    /// <summary>
    /// Path segment prefix for OpenAPI documentation UI routes.
    /// </summary>
    [Required]
    public string DocumentationPathPrefix { get; set; } = "/openapi";
}
