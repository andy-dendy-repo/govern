using Bunit;
using Bunit.JSInterop;
using Govern.Client.Features.Design.Models;
using Govern.Client.Features.Design.Pages;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Design.Pages;

public class BoardBenchPageTests : ClientTestContext
{
    private readonly BunitJSModuleInterop _module;

    public BoardBenchPageTests()
    {
        _module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        _module.Setup<bool>("mount", _ => true).SetResult(true);
        foreach (var call in new[] { "render", "renderOverlay", "renderMotion", "fit", "focus", "zoom", "unmount" })
        {
            _module.SetupVoid(call, _ => true).SetVoidResult();
        }
    }

    [Fact]
    public void Render_Development_DrawsTheLargestMapOnThe3DBoard()
    {
        UseEnvironment("Development");

        var cut = Render<BoardBenchPage>();

        cut.Find("h1").TextContent.ShouldBe("3D board bench");
        var scene = Scenes().Single();
        scene.Tiles.Count.ShouldBe(Ruleset.Map.MaxSize * Ruleset.Map.MaxSize);
        scene.Pieces.Count.ShouldBeGreaterThan(100);
    }

    [Fact]
    public void March_SendsANewSceneWithTheArmyMoved()
    {
        UseEnvironment("Development");
        var cut = Render<BoardBenchPage>();

        cut.Find(".bench-march").Click();

        Scenes().Count.ShouldBe(2);
        Scenes()[1].Pieces.ShouldNotBe(Scenes()[0].Pieces);
    }

    [Fact]
    public void ShowTheOpening_SendsTheFoggedView()
    {
        UseEnvironment("Development");
        var cut = Render<BoardBenchPage>();

        cut.Find(".bench-reveal").Click();

        Scenes()[^1].Pieces.Count.ShouldBe(3);
        Scenes()[^1].Tiles.ShouldContain(static tile => tile.Visibility == "unexplored");
    }

    [Fact]
    public void Measure_ShowsTheBoardsNumbers()
    {
        UseEnvironment("Development");
        _module.Setup<BoardBenchResult?>("measure").SetResult(new BoardBenchResult(
            0, 5.5, new Dictionary<string, double> { ["pieces"] = 2.25 }, 60, 16, 20, 1, 1.5, 129, 62, 1_264_186, 1, 2048,
            new Dictionary<string, int> { ["terrain"] = 93_202 }));
        var cut = Render<BoardBenchPage>();

        cut.Find(".bench-measure").Click();

        var table = cut.Find(".bench-result").TextContent;
        table.ShouldContain("5.5 ms (pieces 2.3 ms)");
        table.ShouldContain("1,264,186");
        table.ShouldContain("terrain 93,202");
        table.ShouldContain("2048 px");
    }

    [Fact]
    public void Measure_NoBoard_SaysSo()
    {
        UseEnvironment("Development");
        _module.Setup<BoardBenchResult?>("measure").SetResult(null);
        var cut = Render<BoardBenchPage>();

        cut.Find(".bench-measure").Click();

        cut.FindAll(".bench-missing").Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Render_OutsideDevelopment_ShowsNotFound(string environment)
    {
        UseEnvironment(environment);

        var cut = Render<BoardBenchPage>();

        cut.Find("h1").TextContent.ShouldBe("Not found");
        cut.FindAll(".bench-page, .board3d").ShouldBeEmpty();
        Scenes().ShouldBeEmpty();
    }

    private List<BoardScene> Scenes() =>
        [.. _module.Invocations.Where(static call => call.Identifier == "render").Select(static call => call.Arguments[1]).OfType<BoardScene>()];

    private void UseEnvironment(string name) =>
        Services.AddSingleton<IWebAssemblyHostEnvironment>(new FakeHostEnvironment(name));

    private sealed class FakeHostEnvironment(string environment) : IWebAssemblyHostEnvironment
    {
        public string Environment { get; } = environment;

        public string BaseAddress => "http://localhost/";
    }
}
