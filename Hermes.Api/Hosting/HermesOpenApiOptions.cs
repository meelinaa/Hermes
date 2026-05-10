namespace Hermes.Api.Hosting;

public sealed class HermesOpenApiOptions
{
    public const string SECTION_NAME = "OpenApi";

    public string DocumentName { get; set; } = "v1";

    public string RoutePattern { get; set; } = "openapi/{documentName}.json";

    public bool MapInProduction { get; set; }

    public string DocumentationApiKey { get; set; } = "";

    public string DocumentationApiKeyHeader { get; set; } = "X-Hermes-Documentation-Key";

    public string DocumentationPathPrefix { get; set; } = "/openapi";
}
