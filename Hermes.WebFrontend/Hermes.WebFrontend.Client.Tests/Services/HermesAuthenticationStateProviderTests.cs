using System.Security.Claims;
using Blazored.LocalStorage;
using Hermes.WebFrontend.Client.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Moq;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Services;

/// <summary>
/// Unit tests verifying JWT claims extraction and state notifications in <see cref="HermesAuthenticationStateProvider"/>.
/// </summary>
public sealed class HermesAuthenticationStateProviderTests
{
    private const string ValidJwt =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJzdWIiOiI0MiIsIm5hbWUiOiJ0ZXN0dXNlciIsImVtYWlsIjoidGVzdEBlbWFpbC5kZSIsInJvbGUiOiJVc2VyIn0." +
        "dummySignature";

    private readonly Mock<ILocalStorageService> _localStorageMock = new();

    private HermesAuthenticationStateProvider CreateSut(string? token = null)
    {
        _localStorageMock.Setup(s => s.GetItemAsync<string>("hermes.auth.accessToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        AuthTokenStore tokenStore = new(_localStorageMock.Object);
        return new HermesAuthenticationStateProvider(tokenStore);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_Should_ReturnAnonymous_When_NoTokenPresent()
    {
        // Arrange
        HermesAuthenticationStateProvider sut = CreateSut(null);

        // Act
        AuthenticationState state = await sut.GetAuthenticationStateAsync();

        // Assert
        Assert.NotNull(state.User);
        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_Should_ReturnAuthenticated_When_ValidJwtToken()
    {
        // Arrange
        HermesAuthenticationStateProvider sut = CreateSut(ValidJwt);

        // Act
        AuthenticationState state = await sut.GetAuthenticationStateAsync();

        // Assert
        Assert.NotNull(state.User);
        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("testuser", state.User.Identity?.Name);
        Assert.Equal("42", state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("test@email.de", state.User.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal("User", state.User.FindFirst(ClaimTypes.Role)?.Value);
    }

    [Fact]
    public void NotifyUserAuthentication_Should_RaiseAuthenticationStateChanged()
    {
        // Arrange
        HermesAuthenticationStateProvider sut = CreateSut(null);
        AuthenticationState? notifiedState = null;
        sut.AuthenticationStateChanged += task =>
        {
            notifiedState = task.GetAwaiter().GetResult();
        };

        // Act
        sut.NotifyUserAuthentication(ValidJwt);

        // Assert
        Assert.NotNull(notifiedState);
        Assert.True(notifiedState.User.Identity?.IsAuthenticated);
        Assert.Equal("42", notifiedState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void NotifyUserLogout_Should_RaiseAuthenticationStateChangedWithAnonymousUser()
    {
        // Arrange
        HermesAuthenticationStateProvider sut = CreateSut(ValidJwt);
        AuthenticationState? notifiedState = null;
        sut.AuthenticationStateChanged += task =>
        {
            notifiedState = task.GetAwaiter().GetResult();
        };

        // Act
        sut.NotifyUserLogout();

        // Assert
        Assert.NotNull(notifiedState);
        Assert.False(notifiedState.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public void ParseClaimsFromJwt_Should_ExtractExpectedClaims()
    {
        // Act
        List<Claim> claims = HermesAuthenticationStateProvider.ParseClaimsFromJwt(ValidJwt).ToList();

        // Assert
        Assert.Contains(claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "42");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Email && c.Value == "test@email.de");
        Assert.Contains(claims, c => c.Type == ClaimTypes.Role && c.Value == "User");
    }

    /// <summary>
    /// Tests that when a refresh token is revoked or session is invalidated, calling <see cref="HermesAuthenticationStateProvider.NotifyUserLogout"/>
    /// transitions the authentication state to anonymous and notifies Blazor cascades to trigger re-login navigation.
    /// </summary>
    [Fact]
    public async Task Blazor_AuthenticationStateProvider_Should_TriggerReLogin_When_RefreshTokenRevoked()
    {
        // Arrange: Start with authenticated user
        HermesAuthenticationStateProvider sut = CreateSut(ValidJwt);
        AuthenticationState initial = await sut.GetAuthenticationStateAsync();
        Assert.True(initial.User.Identity?.IsAuthenticated);

        bool stateChangedFired = false;
        AuthenticationState? updatedState = null;
        sut.AuthenticationStateChanged += task =>
        {
            stateChangedFired = true;
            updatedState = task.GetAwaiter().GetResult();
        };

        // Act: Revocation event / 401 triggers logout notification
        sut.NotifyUserLogout();

        // Assert: User identity is unauthenticated (anonymous)
        Assert.True(stateChangedFired);
        Assert.NotNull(updatedState);
        Assert.False(updatedState.User.Identity?.IsAuthenticated);
    }
}
