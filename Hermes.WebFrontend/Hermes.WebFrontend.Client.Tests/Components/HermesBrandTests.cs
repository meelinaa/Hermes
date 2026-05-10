using Bunit;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Components;

public sealed class HermesBrandTests : BunitContext
{
    [Fact]
    public void Renders_default_title_and_parameter_css_class()
    {
        IRenderedComponent<HermesBrand> cut = Render<HermesBrand>(p => p.Add(c => c.CssClass, "accent"));

        Assert.Contains("hermes-brand accent", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Hermes", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("hermes-brand-icon", cut.Markup, StringComparison.Ordinal);
    }
}
