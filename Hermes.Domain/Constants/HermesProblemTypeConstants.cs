namespace Hermes.Domain.Constants;

/// <summary>
/// Stable <c>ProblemDetails.Type</c> URIs consumed by HTTP clients.
/// </summary>
public static class HermesProblemTypeConstants
{
    /// <summary>
    /// Type URI for wrong current password errors.
    /// </summary>
    public const string WRONG_CURRENT_PASSWORD = "https://hermes.dev/problems/wrong-current-password";
}
