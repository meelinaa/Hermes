namespace Hermes.Domain;

/// <summary>RFC 7807 <c>ProblemDetails.Type</c> values for client-side handling (stable URIs).</summary>
public static class HermesProblemTypes
{
    /// <summary>PUT user profile: <c>currentPassword</c> does not match the stored hash.</summary>
    public const string WrongCurrentPassword = "https://hermes.dev/problems/wrong-current-password";
}
