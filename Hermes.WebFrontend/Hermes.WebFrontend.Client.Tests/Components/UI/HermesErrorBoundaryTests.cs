using Bunit;
using Hermes.WebFrontend.Client.Components.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Components.UI;

/// <summary>
/// Helper throwing component used to simulate runtime rendering faults.
/// </summary>
public sealed class FaultyChildComponent : ComponentBase
{
    [Parameter] public bool ShouldThrow { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (ShouldThrow)
            throw new InvalidOperationException("Simulation: Unerwarteter Render-Fehler!");

        builder.AddMarkupContent(0, "<div id=\"child-ok\">Inhalt erfolgreich gerendert</div>");
    }
}

/// <summary>
/// bUnit tests verifying exception interception, error card display, and recovery in <see cref="HermesErrorBoundary"/>.
/// </summary>
public sealed class HermesErrorBoundaryTests : BunitContext
{
    [Fact]
    public void Renders_ChildContent_When_NoException()
    {
        // Act
        var cut = Render<HermesErrorBoundary>(parameters => parameters
            .AddChildContent<FaultyChildComponent>(child => child.Add(c => c.ShouldThrow, false)));

        // Assert
        Assert.Contains("Inhalt erfolgreich gerendert", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("div.hermes-error-boundary-card"));
    }

    [Fact]
    public void Renders_ErrorCard_When_ExceptionThrown()
    {
        // Act
        var cut = Render<HermesErrorBoundary>(parameters => parameters
            .AddChildContent<FaultyChildComponent>(child => child.Add(c => c.ShouldThrow, true)));

        // Assert
        Assert.Contains("Ein unerwarteter Fehler ist aufgetreten", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Simulation: Unerwarteter Render-Fehler!", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("div.hermes-error-boundary-card"));
    }
}
