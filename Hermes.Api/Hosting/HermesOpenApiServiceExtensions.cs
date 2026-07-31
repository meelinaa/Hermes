using Hermes.Application.DTOs.Login;
using Hermes.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Hermes.Api.Hosting;

public static class HermesOpenApiServiceExtensions
{
    public static IServiceCollection AddHermesOpenApiDocument(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HermesOpenApiOptions>(configuration.GetSection(HermesOpenApiOptions.SECTION_NAME));

        string documentName = configuration.GetSection(HermesOpenApiOptions.SECTION_NAME)
            .Get<HermesOpenApiOptions>()?.DocumentName ?? "v1";

        services.AddOpenApi(documentName, options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
                ApplyDocumentAsync(document, context, configuration, cancellationToken));
            options.AddOperationTransformer(ApplyOperationAsync);
            options.AddSchemaTransformer(ApplySchemaExamplesAsync);
        });

        return services;
    }

    private static async Task ApplyDocumentAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        IConfigurationSection jwt = configuration.GetSection(JwtOptions.SECTION_NAME);
        string issuer = jwt["Issuer"] ?? "Hermes";
        string audience = jwt["Audience"] ?? "Hermes.Api";

        document.Info = new OpenApiInfo
        {
            Title = "Hermes API",
            Version = context.DocumentName,
            Description =
                $"JWT access tokens: HS256, issuer `{issuer}`, audience `{audience}`. " +
                "Send `Authorization: Bearer <accessToken>` on protected routes. " +
                "Errors use `application/problem+json` (RFC 7807). Shared models: " +
                "`ProblemDetails` for most failures, `ValidationProblemDetails` (and `errors`) for FluentValidation."
        };

        OpenApiComponents components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        components.Responses ??= new Dictionary<string, IOpenApiResponse>();

        components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = $"HS256 JWT issued by this API. Issuer: `{issuer}`, Audience: `{audience}`."
        };

        OpenApiSchema problemDetails = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, cancellationToken)
            .ConfigureAwait(false);
        OpenApiSchema validationProblem = await context.GetOrCreateSchemaAsync(typeof(ValidationProblemDetails), null, cancellationToken)
            .ConfigureAwait(false);

        components.Schemas["ProblemDetails"] = problemDetails;
        components.Schemas["ValidationProblemDetails"] = validationProblem;

        components.Responses["ProblemDetails"] = ProblemResponse(document, "ProblemDetails", "RFC 7807 problem details.");
        components.Responses["ValidationProblemDetails"] = ProblemResponse(document, "ValidationProblemDetails", "Validation failure (RFC 7807 + `errors` map).");
    }

    private static OpenApiResponse ProblemResponse(OpenApiDocument document, string schemaName, string description) =>
        new()
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchemaReference(schemaName, document, null)
                }
            }
        };

    private static Task ApplyOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Document is not { } document)
            return Task.CompletedTask;

        operation.Responses ??= new OpenApiResponses();

        operation.Responses.TryAdd("400", new OpenApiResponse
        {
            Description = "Bad request or validation failure.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchemaReference(
                        HttpMethodRequiresRequestBody(context.Description) ? "ValidationProblemDetails" : "ProblemDetails",
                        document,
                        null)
                }
            }
        });

        if (EndpointRequiresAuthenticatedUser(context.Description))
        {
            var schemeRef = new OpenApiSecuritySchemeReference("Bearer", document, null);
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement { [schemeRef] = [] });

            operation.Responses.TryAdd("401", new OpenApiResponse
            {
                Description = "Missing or invalid Bearer token (RFC 7807).",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference("ProblemDetails", document, null)
                    }
                }
            });
        }

        return Task.CompletedTask;
    }

    private static bool HttpMethodRequiresRequestBody(ApiDescription description)
    {
        string m = description.HttpMethod?.ToUpperInvariant() ?? "";
        return m is "POST" or "PUT" or "PATCH";
    }

    private static bool EndpointRequiresAuthenticatedUser(ApiDescription api)
    {
        IList<object>? meta = api.ActionDescriptor?.EndpointMetadata;
        if (meta is null || meta.Count == 0)
            return false;

        if (meta.Any(static m => m is IAllowAnonymous))
            return false;

        return meta.OfType<IAuthorizeData>().Any();
    }

    private static Task ApplySchemaExamplesAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken _)
    {
        Type? type = context.JsonTypeInfo?.Type;
        if (type == typeof(LoginRequest))
        {
            schema.Example = new JsonObject
            {
                ["nameOrEmail"] = "max@example.com",
                ["password"] = "(plain password)"
            };
            return Task.CompletedTask;
        }

        if (type == typeof(RefreshRequest))
        {
            schema.Example = new JsonObject { ["refreshToken"] = "(opaque refresh token from login)" };
            return Task.CompletedTask;
        }

        if (type == typeof(LogoutRequest))
        {
            schema.Example = new JsonObject { ["refreshToken"] = "(optional; omit to revoke all sessions)" };
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
