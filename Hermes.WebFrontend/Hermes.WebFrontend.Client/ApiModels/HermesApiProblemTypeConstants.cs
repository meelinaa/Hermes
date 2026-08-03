namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>RFC 7807 <c>ProblemDetails.Type</c> values mirrored from the API for client handling.</summary>
public static class HermesApiProblemTypeConstants
{
    /// <summary>PUT user profile: <c>currentPassword</c> does not match the stored hash.</summary>
    public const string WRONG_CURRENT_PASSWORD = "https://hermes.dev/problems/wrong-current-password";
}
