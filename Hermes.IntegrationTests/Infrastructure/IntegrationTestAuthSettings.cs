namespace Hermes.IntegrationTests.Infrastructure;

/// <summary>
/// JWT signing parameters mirrored in <see cref="HermesApiWebApplicationFactory"/> so integration tests can mint adversarial tokens
/// (expired, wrong issuer, wrong signing material) that align with what <see cref="Hermes.Api.Hosting.JwtAuthenticationExtensions"/> validates at runtime.
/// </summary>
internal static class IntegrationTestAuthSettings
{
    public const string JwtIssuer = "Hermes.IntegrationTests";

    public const string JwtAudience = "Hermes.Api.Tests";

    /// <summary>Same minimum-length symmetric secret configured on the test API host (HS256).</summary>
    public const string JwtSigningKey = "INTEGRATION_TESTS_SIGNING_KEY_32_CHARS_MIN________";
}
