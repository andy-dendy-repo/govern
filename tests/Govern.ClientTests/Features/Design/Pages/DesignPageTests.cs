using Govern.Client.Features.Design.Pages;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Design.Pages;

public class DesignPageTests : ClientTestContext
{
    [Fact]
    public void Render_Development_ShowsTheCatalogWithEveryRulesetIcon()
    {
        UseEnvironment("Development");

        var cut = Render<DesignPage>();

        cut.Find("h1").TextContent.ShouldBe("Design system");
        cut.FindAll(".design-icons use").Count.ShouldBeGreaterThanOrEqualTo(
            TestViews.Ruleset.Units.Length + TestViews.Ruleset.Terrains.Length + TestViews.Ruleset.Improvements.Length
            + TestViews.Ruleset.Buildings.Length + TestViews.Ruleset.Resources.Length);
        cut.FindAll(".stat-table, .progress, .cost-label, .confirm-button").Count.ShouldBe(4);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Render_OutsideDevelopment_ShowsNotFound(string environment)
    {
        UseEnvironment(environment);

        var cut = Render<DesignPage>();

        cut.Find("h1").TextContent.ShouldBe("Not found");
        cut.FindAll(".design-page, svg.game-sprite").ShouldBeEmpty();
    }

    private void UseEnvironment(string name) =>
        Services.AddSingleton<IWebAssemblyHostEnvironment>(new FakeHostEnvironment(name));

    private sealed class FakeHostEnvironment(string environment) : IWebAssemblyHostEnvironment
    {
        public string Environment { get; } = environment;

        public string BaseAddress => "http://localhost/";
    }
}
