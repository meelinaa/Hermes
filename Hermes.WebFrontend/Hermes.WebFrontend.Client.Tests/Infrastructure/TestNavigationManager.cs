using Microsoft.AspNetCore.Components;

namespace Hermes.WebFrontend.Client.Tests.Infrastructure;

/// <summary>
/// In-memory test implementation of <see cref="NavigationManager"/> tracking navigation destinations.
/// </summary>
public sealed class TestNavigationManager : NavigationManager
{
    /// <summary>Creates and initializes the test navigation manager at the root URL.</summary>
    public TestNavigationManager()
    {
        Initialize("http://localhost/", "http://localhost/");
    }

    /// <summary>Gets the target URI of the most recent navigation call.</summary>
    public string? LastNavigatedUri { get; private set; }

    /// <summary>Gets whether the most recent navigation requested a force load.</summary>
    public bool LastForceLoad { get; private set; }

    /// <summary>Captures navigation calls in memory.</summary>
    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
        LastNavigatedUri = uri;
        LastForceLoad = options.ForceLoad;
        Uri = ToAbsoluteUri(uri).ToString();
    }
}
