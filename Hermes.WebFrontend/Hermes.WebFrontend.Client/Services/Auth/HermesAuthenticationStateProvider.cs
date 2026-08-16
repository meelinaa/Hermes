using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Hermes.WebFrontend.Client.Services.Auth;

/// <summary>
/// Custom authentication state provider integrating JWT token storage with Blazor's cascading authorization state.
/// </summary>
public sealed class HermesAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState _anonymousState = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly AuthTokenStore _tokenStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="HermesAuthenticationStateProvider"/> class and subscribes to token changes.
    /// </summary>
    public HermesAuthenticationStateProvider(AuthTokenStore tokenStore)
    {
        _tokenStore = tokenStore;
        _tokenStore.AuthenticationStateChanged += OnTokenStoreAuthenticationStateChanged;
    }

    private void OnTokenStoreAuthenticationStateChanged(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            NotifyUserLogout();
        else
            NotifyUserAuthentication(token);
    }

    /// <summary>
    /// Evaluates the current authentication state from persisted tokens.
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await _tokenStore.EnsureLoadedFromStorageAsync().ConfigureAwait(false);
        string? token = _tokenStore.AccessToken;

        if (string.IsNullOrWhiteSpace(token))
            return _anonymousState;

        try
        {
            IEnumerable<Claim> claims = ParseClaimsFromJwt(token);
            ClaimsIdentity identity = new(claims, "Bearer");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return _anonymousState;
        }
    }

    /// <summary>
    /// Notifies Blazor components that a user has successfully signed in.
    /// </summary>
    public void NotifyUserAuthentication(string token)
    {
        IEnumerable<Claim> claims = ParseClaimsFromJwt(token);
        ClaimsIdentity identity = new(claims, "Bearer");
        ClaimsPrincipal user = new(identity);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    /// <summary>
    /// Notifies Blazor components that the current user has logged out.
    /// </summary>
    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(_anonymousState));
    }

    /// <summary>
    /// Parses claims from the base64-url encoded payload of a JWT.
    /// </summary>
    public static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        List<Claim> claims = [];
        string[] parts = jwt.Split('.');
        if (parts.Length < 2)
            return claims;

        string payload = parts[1];
        string padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
                               .Replace('-', '+')
                               .Replace('_', '/');

        byte[] bytes = Convert.FromBase64String(padded);
        using JsonDocument doc = JsonDocument.Parse(bytes);

        foreach (JsonProperty property in doc.RootElement.EnumerateObject())
        {
            switch (property.Name)
            {
                case "sub":
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, property.Value.GetString() ?? string.Empty));
                    break;
                case "name":
                case "unique_name":
                    claims.Add(new Claim(ClaimTypes.Name, property.Value.GetString() ?? string.Empty));
                    break;
                case "email":
                    claims.Add(new Claim(ClaimTypes.Email, property.Value.GetString() ?? string.Empty));
                    break;
                case "role":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement element in property.Value.EnumerateArray())
                            claims.Add(new Claim(ClaimTypes.Role, element.GetString() ?? string.Empty));
                    }
                    else
                    {
                        claims.Add(new Claim(ClaimTypes.Role, property.Value.GetString() ?? string.Empty));
                    }
                    break;
                default:
                    if (property.Value.ValueKind == JsonValueKind.String)
                        claims.Add(new Claim(property.Name, property.Value.GetString() ?? string.Empty));
                    break;
            }
        }

        return claims;
    }

    /// <summary>Unsubscribes from token store events.</summary>
    public void Dispose()
    {
        _tokenStore.AuthenticationStateChanged -= OnTokenStoreAuthenticationStateChanged;
    }
}
