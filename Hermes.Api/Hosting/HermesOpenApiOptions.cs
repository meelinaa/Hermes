namespace Hermes.Api.Hosting;

/// <summary>Controls versioned OpenAPI document exposure (route, production visibility, optional shared secret).</summary>
public sealed class HermesOpenApiOptions
{
    public const string SECTION_NAME = "OpenApi";

    /// <summary>OpenAPI document name (appears in <c>{documentName}</c> route segment).</summary>
    public string DocumentName { get; set; } = "v1";

    /// <summary>Route template for the generated document; must include <c>{documentName}</c>.</summary>
    public string RoutePattern { get; set; } = "openapi/{documentName}.json";

    /// <summary>When true, the OpenAPI JSON is served in Production. Otherwise it is only served outside Production (e.g. Development, Testing).</summary>
    public bool MapInProduction { get; set; }

    /// <summary>Optional shared secret: when set in Production, clients must send it in <see cref="DocumentationApiKeyHeader"/>.</summary>
    public string DocumentationApiKey { get; set; } = "";

    public string DocumentationApiKeyHeader { get; set; } = "X-Hermes-Documentation-Key";

    /// <summary>
    /// URL path prefix for the documentation middleware (starts with '/', e.g. <c>/openapi</c> or <c>/internal/openapi</c>).
    /// Must align with how <see cref="RoutePattern"/> is exposed via routing.
    /// </summary>
    public string DocumentationPathPrefix { get; set; } = "/openapi";
}
