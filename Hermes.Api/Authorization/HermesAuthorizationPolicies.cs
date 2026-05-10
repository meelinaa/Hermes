namespace Hermes.Api.Authorization;

/// <summary>ASP.NET Core authorization policy names: route parameter must match JWT <c>nameidentifier</c>.</summary>
public static class HermesAuthorizationPolicies
{
    /// <summary>Requires <c>{userId}</c> in route to match the authenticated user id.</summary>
    public const string OWN_USER_ROUTE_USER_ID = "Hermes.OwnUser:userId";

    /// <summary>Requires <c>{id}</c> in route to match the authenticated user id (user resource routes).</summary>
    public const string OWN_USER_ROUTE_ID = "Hermes.OwnUser:id";
}
