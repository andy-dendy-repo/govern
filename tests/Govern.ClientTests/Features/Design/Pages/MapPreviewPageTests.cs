using Bunit;
using Bunit.JSInterop;
using Govern.Client.Features.Design.Pages;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Govern.ClientTests.Features.Design.Pages;

public class MapPreviewPageTests : ClientTestContext
{
    private readonly BunitJSModuleInterop _module;

    public MapPreviewPageTests()
    {
        _module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        _module.Setup<bool>("mount", _ => true).SetResult(true);
        foreach (var call in new[] { "render", "renderOverlay", "renderMotion", "fit", "focus", "zoom", "unmount" })
        {
            _module.SetupVoid(call, _ => true).SetVoidResult();
        }
    }

    [Fact]
    public void Render_Development_DrawsTheFirstPoolMap()
    {
        UseEnvironment("Development");

        var cut = Render<MapPreviewPage>();

        cut.Find("h1").TextContent.ShouldBe("Map preview");
        cut.Find(".preview-size").TextContent.ShouldContain("32×20, 2 players, spawn candidates: 3, 3");
        var scene = Scenes().Single();
        scene.Tiles.Count.ShouldBe(32 * 20);
        scene.Pieces.Count.ShouldBe(6);
    }

    [Fact]
    public void Render_Production_ShowsNotFound()
    {
        UseEnvironment("Production");

        var cut = Render<MapPreviewPage>();

        cut.Find(".not-found").TextContent.ShouldContain("does not exist");
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
