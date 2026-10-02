using System.Reflection;
using Bunit.JSInterop;
using Govern.Client.Features.Games.Components.Board;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Govern.Core.Map;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components.Board;

public class GameBoard3DTests : ClientTestContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly List<HexCoord> _clicked = [];

    public GameBoard3DTests()
    {
        _module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        _module.Setup<bool>("mount", _ => true).SetResult(true);
        foreach (var call in new[] { "render", "fit", "focus", "zoom", "unmount" })
        {
            _module.SetupVoid(call, _ => true).SetVoidResult();
        }
    }

    [Fact]
    public void Render_Mounted_DrawsTheViewsSceneAndOpensOnThePlayersPieces()
    {
        var view = Start();

        RenderBoard(view);

        var scene = Calls("render").Single().Arguments[1].ShouldBeOfType<BoardScene>();
        scene.Tiles.Count.ShouldBe(Width * Height);
        Calls("fit").Single().Arguments[1].ShouldBe(BoardSceneBuilder.Build(view).Start);
    }

    [Fact]
    public void Render_SameView_IsNotSentAgain()
    {
        var view = Start();
        var cut = RenderBoard(view);

        cut.Render(parameters => parameters.Add(static board => board.Disabled, true));

        Calls("render").Count.ShouldBe(1);
    }

    [Fact]
    public void Render_NewView_SendsItsScene()
    {
        var cut = RenderBoard(Start());

        cut.Render(parameters => parameters.Add(static board => board.View, Create(visibility: static _ => TileVisibility.Explored) with { Version = TestViews.Version + 1 }));

        Calls("render").Count.ShouldBe(2);
        Calls("render")[1].Arguments[1].ShouldBeOfType<BoardScene>().Tiles.ShouldAllBe(static tile => tile.Visibility == "explored");
    }

    [Fact]
    public void Render_SameVersionAgain_IsNotSentAgain()
    {
        var cut = RenderBoard(Start());

        cut.Render(parameters => parameters.Add(static board => board.View, Start()));

        Calls("render").Count.ShouldBe(1);
    }

    [Fact]
    public void Render_FocusFromBeforeTheBoard_IsNotFollowed()
    {
        Render<GameBoard3D>(parameters => parameters
            .Add(static board => board.View, Start())
            .Add(static board => board.Focus, new BoardFocus(At(7, 5), 3)));

        _module.Invocations["focus"].ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_Board_UnmountsTheRenderer()
    {
        RenderBoard(Start());

        await DisposeComponentsAsync();

        Calls("unmount").Count.ShouldBe(1);
    }

    [Fact]
    public void Render_WebGlUnavailable_SaysSoAndSendsNothing()
    {
        _module.Setup<bool>("mount", _ => true).SetResult(false);

        var cut = RenderBoard(Start());

        cut.Find(".board3d-unavailable").TextContent.ShouldContain("can't show the 3D board");
        Calls("render").ShouldBeEmpty();
    }

    [Fact]
    public async Task Click_TileInTheRenderer_RaisesTileClicked()
    {
        RenderBoard(Start());

        await ClickInRendererAsync(At(3, 2));

        _clicked.ShouldBe([At(3, 2)]);
    }

    [Fact]
    public async Task Click_WhileDisabled_IsIgnored()
    {
        RenderBoard(Start(), disabled: true);

        await ClickInRendererAsync(At(3, 2));

        _clicked.ShouldBeEmpty();
    }

    [Fact]
    public void Render_NewFocus_CentresTheCameraOnTheTile()
    {
        var cut = RenderBoard(Start());

        cut.Render(parameters => parameters.Add(static board => board.Focus, new BoardFocus(At(5, 4), 1)));

        var (x, z) = BoardSceneBuilder.Center(At(5, 4));
        Calls("focus").Single().Arguments.Skip(1).ShouldBe([x, z]);
    }

    [Fact]
    public void Click_ZoomAndFitButtons_MoveTheCamera()
    {
        var cut = RenderBoard(Start());

        cut.Find(".zoom-in").Click();
        cut.Find(".zoom-fit").Click();

        Calls("zoom").Single().Arguments[1].ShouldBe(GameBoard.ZoomStep);
        Calls("fit")[^1].Arguments[1].ShouldBe(BoardSceneBuilder.Build(Start()).Map);
    }

    private IReadOnlyList<JSRuntimeInvocation> Calls(string identifier) => _module.Invocations[identifier];

    private IRenderedComponent<GameBoard3D> RenderBoard(PlayerView view, bool disabled = false) =>
        Render<GameBoard3D>(parameters => parameters
            .Add(static board => board.View, view)
            .Add(static board => board.Disabled, disabled)
            .Add(static board => board.TileClicked, coord => _clicked.Add(coord)));

    // What the renderer does on a click: it calls OnTileClicked on the .NET object it was mounted with.
    private async Task ClickInRendererAsync(HexCoord coord)
    {
        var relay = Calls("mount").Single().Arguments[1]!;
        var target = relay.GetType().GetProperty("Value")!.GetValue(relay)!;
        var method = target.GetType().GetMethod("OnTileClicked", BindingFlags.Public | BindingFlags.Instance)!;
        await (Task)method.Invoke(target, [coord.Q, coord.R])!;
    }
}
