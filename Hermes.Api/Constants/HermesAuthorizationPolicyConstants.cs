namespace Hermes.Api.Constants;

/// <summary>
/// Defines policy name constants for authorization checks across API endpoints.
/// </summary>
public static class HermesAuthorizationPolicyConstants
{
    /// <summary>
    /// Policy key requiring matching userId claim against route parameter 'userId'.
    /// </summary>
    public const string OWN_USER_ROUTE_USER_ID = "Hermes.OwnUser:userId";

    /// <summary>
    /// Policy key requiring matching userId claim against route parameter 'id'.
    /// </summary>
    public const string OWN_USER_ROUTE_ID = "Hermes.OwnUser:id";
}
