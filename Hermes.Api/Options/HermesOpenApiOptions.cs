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
    /// Gets or sets the OpenAPI document name.
    /// </summary>
    public string DocumentName { get; set; } = "v1";

    /// <summary>
    /// Gets or sets the route pattern for the OpenAPI JSON document.
    /// </summary>
    public string RoutePattern { get; set; } = "openapi/{documentName}.json";

    /// <summary>
    /// Gets or sets a value indicating whether OpenAPI documentation endpoints are enabled in Production.
    /// </summary>
    public bool MapInProduction { get; set; }

    /// <summary>
    /// Gets or sets the API key required for accessing documentation endpoints.
    /// </summary>
    public string DocumentationApiKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the header name used for transmitting the documentation API key.
    /// </summary>
    public string DocumentationApiKeyHeader { get; set; } = "X-Hermes-Documentation-Key";

    /// <summary>
    /// Gets or sets the path prefix for OpenAPI documentation UI endpoints.
    /// </summary>
    public string DocumentationPathPrefix { get; set; } = "/openapi";
}
