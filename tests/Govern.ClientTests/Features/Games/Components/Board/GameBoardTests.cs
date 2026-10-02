using System.Reflection;
using Bunit.JSInterop;
using Govern.Client.Features.Games.Components.Board;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Govern.Core.Map;
using Govern.Core.Units;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components.Board;

public class GameBoardTests : ClientTestContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly List<HexCoord> _clicked = [];

    public GameBoardTests()
    {
        _module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        _module.Setup<bool>("mount", _ => true).SetResult(true);
        foreach (var call in new[] { "render", "renderOverlay", "renderMotion", "fit", "focus", "zoom", "unmount" })
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
        Render<GameBoard>(parameters => parameters
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

        cut.Find(".board3d-unavailable").TextContent.ShouldContain("can't show the game board");
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

    [Fact]
    public void Render_Mounted_SendsAnEmptyOverlay()
    {
        RenderBoard(Start());

        Calls("renderOverlay").Single().Arguments[1].ShouldBe(BoardOverlay.None);
    }

    [Fact]
    public void Render_NewSelection_SendsAnOverlayButNoScene()
    {
        var cut = RenderBoard(Start());

        cut.Render(parameters => parameters
            .Add(static board => board.Selection, BoardSelection.Of(new UnitId(1)))
            .Add(static board => board.Highlights, BoardHighlights.None with { Reachable = [At(1, 1)] }));

        Calls("render").Count.ShouldBe(1);
        var overlay = Calls("renderOverlay")[^1].Arguments[1].ShouldBeOfType<BoardOverlay>();
        overlay.Selected.ShouldNotBeNull();
        overlay.Tiles.ShouldHaveSingleItem().Tint.ShouldBe("--gv-overlay-reachable");
    }

    [Fact]
    public void Render_SameSelectionAndHighlights_SendsNoOverlay()
    {
        var highlights = BoardHighlights.None with { Reachable = [At(1, 1)] };
        var cut = RenderBoard(Start());
        cut.Render(parameters => parameters.Add(static board => board.Selection, BoardSelection.Of(new UnitId(1))).Add(static board => board.Highlights, highlights));

        cut.Render(parameters => parameters.Add(static board => board.Selection, BoardSelection.Of(new UnitId(1))).Add(static board => board.Disabled, true));

        Calls("renderOverlay").Count.ShouldBe(2);
    }

    [Fact]
    public void Render_NewView_SendsItsOverlayAgain()
    {
        var cut = RenderBoard(Start());

        cut.Render(parameters => parameters.Add(static board => board.View, Start() with { Version = TestViews.Version + 1 }));

        Calls("renderOverlay").Count.ShouldBe(2);
    }

    [Fact]
    public void Render_NewLensOnly_SendsOneOverlayAndNoScene()
    {
        var cut = RenderBoard(Start());

        cut.Render(parameters => parameters.Add(static board => board.Lens, new MovementCostLens(new RulesetService())));

        Calls("render").Count.ShouldBe(1);
        Calls("renderOverlay").Count.ShouldBe(2);
    }

    [Fact]
    public void Render_Lens_SendsItsMarksWithLabels()
    {
        var cut = RenderBoard(Start());

        cut.Render(parameters => parameters.Add(static board => board.Lens, new MovementCostLens(new RulesetService())));

        Calls("renderOverlay")[^1].Arguments[1].ShouldBeOfType<BoardOverlay>().Tiles.Count(static tile => tile.Label is not null).ShouldBe(Width * Height);
    }

    [Fact]
    public void Render_MaxHealth_GivesHurtUnitsTheirHealthShare()
    {
        Render<GameBoard>(parameters => parameters
            .Add(static board => board.View, Create([Soldier(2, You, At(3, 2), health: 50)]))
            .Add(static board => board.MaxHealth, 100));

        Calls("render").Single().Arguments[1].ShouldBeOfType<BoardScene>().Pieces.ShouldHaveSingleItem().Health.ShouldBe(0.5);
    }

    [Fact]
    public void Render_Mounted_SendsAnEmptyMotionAfterTheScene()
    {
        RenderBoard(Start());

        var motion = Calls("renderMotion").Single().Arguments[1].ShouldBeOfType<SceneMotion>();
        (motion.Positions.Count, motion.Ghosts.Count, motion.Effects.Count).ShouldBe((0, 0, 0));
        _module.Invocations.Select(static call => call.Identifier).ToList().IndexOf("renderMotion")
            .ShouldBeGreaterThan(_module.Invocations.Select(static call => call.Identifier).ToList().IndexOf("render"));
    }

    [Fact]
    public void Render_NewMotion_SendsItInSceneUnitsButNoScene()
    {
        var cut = RenderBoard(Start());
        var (x, y) = HexLayout.Center(At(4, 2));

        cut.Render(parameters => parameters.Add(static board => board.Motion, BoardMotion.None with
        {
            Positions = BoardMotion.None.Positions.Add(new UnitId(2), new BoardPoint(x, y)),
        }));

        Calls("render").Count.ShouldBe(1);
        var position = Calls("renderMotion")[^1].Arguments[1].ShouldBeOfType<SceneMotion>().Positions.ShouldHaveSingleItem();
        var (sceneX, sceneZ) = BoardSceneBuilder.Center(At(4, 2));
        (position.Unit, position.X, position.Z).ShouldBe((2, sceneX, sceneZ));
    }

    [Fact]
    public void Render_SameMotion_IsNotSentAgain()
    {
        var motion = BoardMotion.None with { Ghosts = [new BoardGhost(1, Opponent, SoldierType, new BoardPoint(0, 0), Fading: true)] };
        var cut = RenderBoard(Start());
        cut.Render(parameters => parameters.Add(static board => board.Motion, motion));

        cut.Render(parameters => parameters.Add(static board => board.Motion, motion).Add(static board => board.Disabled, true));

        Calls("renderMotion").Count.ShouldBe(2);
    }

    private IReadOnlyList<JSRuntimeInvocation> Calls(string identifier) => _module.Invocations[identifier];

    private IRenderedComponent<GameBoard> RenderBoard(PlayerView view, bool disabled = false) =>
        Render<GameBoard>(parameters => parameters
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
