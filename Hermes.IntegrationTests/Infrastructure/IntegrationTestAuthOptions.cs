namespace Hermes.IntegrationTests.Infrastructure;

/// <summary>JWT settings aligned with the test host so minted tokens pass the same validation as production code paths.</summary>
internal static class IntegrationTestAuthOptions
{
    public const string JWT_ISSUER = "Hermes.IntegrationTests";

    public const string JWT_AUDIENCE = "Hermes.Api.Tests";

    public const string JWT_SIGNING_KEY = "INTEGRATION_TESTS_SIGNING_KEY_32_CHARS_MIN________";
}
