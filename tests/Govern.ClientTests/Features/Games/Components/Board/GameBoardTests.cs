using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Govern.Client.Features.Games.Components.Board;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Lenses;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Govern.Core.Map;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components.Web;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components.Board;

public partial class GameBoardTests : ClientTestContext
{
    private readonly List<HexCoord> _clicked = [];

    public GameBoardTests()
    {
        var module = JSInterop.SetupModule(BoardInteropService.ModulePath);
        module.SetupVoid("init", _ => true).SetVoidResult();
        module.Setup<BoardRect?>("measure", _ => true).SetResult(new BoardRect(0, 0, 800, 600));
    }

    [Fact]
    public void Render_View_DrawsEveryTileWithWhatThePlayerKnows()
    {
        var view = Create(visibility: static coord => coord.R switch
        {
            0 => TileVisibility.Unexplored,
            1 => TileVisibility.Explored,
            _ => TileVisibility.Visible,
        });

        var cut = RenderBoard(view);

        cut.FindAll("use.tile").Count.ShouldBe(Width * Height);
        cut.FindAll("use.tile.unexplored").Count.ShouldBe(Width);
        cut.FindAll("use.tile.explored").Count.ShouldBe(Width);
        cut.FindAll("use.tile.visible.terrain-plains").Count.ShouldBe(Width * (Height - 2));
    }

    [Fact]
    public void Render_Pieces_AreMarkedOwnOrEnemyWithCityNamesAsText()
    {
        var view = Create([Settler(1, You, At(2, 2)), Soldier(3, Opponent, At(5, 2))], [City(8, Opponent, At(5, 4), "<b>Aria</b>")]);

        var cut = RenderBoard(view);

        cut.FindAll("use.unit.settler.own").Count.ShouldBe(1);
        cut.FindAll("use.unit.soldier.enemy").Count.ShouldBe(1);
        cut.Find("g.city.enemy text.city-name").TextContent.ShouldBe("<b>Aria</b>");
        cut.FindAll("b").ShouldBeEmpty();
    }

    [Fact]
    public void Render_Pieces_AreColouredBySeatAndDrawnFromTheIconSet()
    {
        var view = Create([Settler(1, You, At(2, 2)), Soldier(3, Opponent, At(5, 2))], [City(8, Opponent, At(5, 4), "Aria")]);

        var cut = RenderBoard(view);

        cut.Find("use.unit.settler").ClassList.ShouldContain("seat-1");
        cut.Find("use.unit.settler").GetAttribute("href").ShouldBe("#unit-settler");
        cut.Find("use.unit.soldier").ClassList.ShouldContain("seat-2");
        cut.Find("g.city").ClassList.ShouldContain("seat-2");
        cut.Find("g.city use").GetAttribute("href").ShouldBe("#city");
    }

    [Fact]
    public void Render_Layers_FollowTheBoardLayerOrder()
    {
        var cut = RenderBoard(Start());

        var rendered = cut.FindAll("g[data-layer]").Select(static layer => layer.GetAttribute("data-layer")!).ToList();

        rendered.ShouldNotBeEmpty();
        var order = Enum.GetNames<BoardLayer>().Select(static name => name.ToLowerInvariant()).ToList();
        rendered.ShouldAllBe(layer => order.Contains(layer));
        rendered.Select(layer => order.IndexOf(layer)).ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public void Render_KnownTerrain_DrawsItsSymbolExceptForTheDefaultTerrain()
    {
        var view = Create(
            visibility: static coord => coord.R == 0 ? TileVisibility.Unexplored : coord.R == 1 ? TileVisibility.Explored : TileVisibility.Visible,
            terrain: static coord => coord.Column == 3 ? Forest : coord.Column == 4 ? Mountains : Plains);

        var cut = RenderBoard(view);

        var symbols = cut.FindAll("use.terrain-symbol");
        symbols.Count.ShouldBe(2 * (Height - 1));
        cut.FindAll("use.terrain-symbol.terrain-forest.explored").Count.ShouldBe(1);
        cut.FindAll("use.terrain-symbol.terrain-mountains.visible").Count.ShouldBe(Height - 2);
        cut.Find("use.terrain-symbol.terrain-forest").GetAttribute("href").ShouldBe("#terrain-forest");
    }

    [Fact]
    public void Render_NoLens_DrawsNoLensLayer()
    {
        var cut = RenderBoard(Start());

        cut.FindAll("g[data-layer=lens]").ShouldBeEmpty();
    }

    [Fact]
    public void Render_MovementCostLens_MarksEveryKnownTileWithItsCost()
    {
        var view = Create(
            visibility: static coord => coord.R == 0 ? TileVisibility.Unexplored : TileVisibility.Visible,
            terrain: static coord => coord.Column == 3 ? Forest : coord.Column == 4 ? Mountains : Plains);

        var cut = RenderBoard(view, lens: new MovementCostLens(new RulesetService()));

        cut.Find("g[data-layer=lens]").ClassList.ShouldContain("lens-movement-cost");
        cut.FindAll("g.lens-mark").Count.ShouldBe(Width * (Height - 1));
        cut.FindAll("g.lens-mark.costly text").Select(static text => text.TextContent).ShouldAllBe(label => label == "2");
        cut.FindAll("g.lens-mark.blocked").Count.ShouldBe(Height - 1);
        cut.Find("g.lens-mark.blocked title").TextContent.ShouldBe("Mountains: land units can't enter");
    }

    [Fact]
    public void Render_Lens_SitsBetweenTheTerrainAndTheHighlights()
    {
        var cut = RenderBoard(Start(), highlights: new BoardHighlights([At(1, 1)], []), lens: new MovementCostLens(new RulesetService()));

        cut.FindAll("g[data-layer]").Select(static layer => layer.GetAttribute("data-layer")).ShouldBe(["terrain", "lens", "highlights", "cities", "units"]);
    }

    [Fact]
    public void Render_SelectionAndHighlights_AreMarked()
    {
        var highlights = new BoardHighlights([At(1, 1), At(1, 2)], [At(4, 2)]);

        var cut = RenderBoard(Start(), BoardSelection.Of(new UnitId(1)), highlights);

        cut.FindAll("use.unit.selected").Count.ShouldBe(1);
        cut.FindAll("use.overlay.reachable").Count.ShouldBe(2);
        cut.FindAll("use.overlay.attackable").Count.ShouldBe(1);
    }

    [Fact]
    public void Click_Tile_RaisesTheTile()
    {
        var cut = RenderBoard(Start());

        Tile(cut, At(1, 1)).Click();

        _clicked.ShouldBe([At(1, 1)]);
    }

    [Fact]
    public void Click_Disabled_IsIgnored()
    {
        var cut = RenderBoard(Start(), disabled: true);

        Tile(cut, At(1, 1)).Click();

        _clicked.ShouldBeEmpty();
    }

    [Fact]
    public void Drag_Pressed_PansAndSwallowsTheClick()
    {
        var cut = RenderBoard(Start());
        var before = Transform(cut);

        Press(cut, 100, 100);
        cut.Find("svg").PointerMove(new PointerEventArgs { ClientX = 140, ClientY = 90, Buttons = 1, IsPrimary = true });
        cut.Find("svg").PointerUp(new PointerEventArgs { ClientX = 140, ClientY = 90, IsPrimary = true });
        Tile(cut, At(1, 1)).Click();

        Transform(cut).X.ShouldBe(before.X + 40, 0.01);
        Transform(cut).Y.ShouldBe(before.Y - 10, 0.01);
        _clicked.ShouldBeEmpty();

        Press(cut, 100, 100);
        Tile(cut, At(1, 1)).Click();
        _clicked.ShouldBe([At(1, 1)]);
    }

    [Fact]
    public void Drag_WithinTheThreshold_StillClicks()
    {
        var cut = RenderBoard(Start());
        var before = Transform(cut);

        Press(cut, 100, 100);
        cut.Find("svg").PointerMove(new PointerEventArgs { ClientX = 102, ClientY = 101, Buttons = 1, IsPrimary = true });
        Tile(cut, At(1, 1)).Click();

        Transform(cut).ShouldBe(before);
        _clicked.ShouldBe([At(1, 1)]);
    }

    [Fact]
    public void Drag_LeavesTheBoard_EndsThePan()
    {
        var cut = RenderBoard(Start());
        Press(cut, 100, 100);
        cut.Find("svg").PointerMove(new PointerEventArgs { ClientX = 150, ClientY = 100, Buttons = 1, IsPrimary = true });
        var afterDrag = Transform(cut);

        cut.Find("svg").PointerLeave(new PointerEventArgs { ClientX = 150, ClientY = 100, IsPrimary = true });
        cut.Find("svg").PointerMove(new PointerEventArgs { ClientX = 250, ClientY = 100, Buttons = 1, IsPrimary = true });

        Transform(cut).ShouldBe(afterDrag);
    }

    [Fact]
    public void Drag_SecondFinger_IsIgnored()
    {
        var cut = RenderBoard(Start());
        var before = Transform(cut);
        Press(cut, 100, 100);

        cut.Find("svg").PointerDown(new PointerEventArgs { PointerId = 2, ClientX = 400, ClientY = 400, Buttons = 1, IsPrimary = false });
        cut.Find("svg").PointerMove(new PointerEventArgs { PointerId = 2, ClientX = 500, ClientY = 500, Buttons = 1, IsPrimary = false });
        cut.Find("svg").PointerMove(new PointerEventArgs { ClientX = 110, ClientY = 100, Buttons = 1, IsPrimary = true });

        Transform(cut).X.ShouldBe(before.X + 10, 0.01);
        Transform(cut).Y.ShouldBe(before.Y, 0.01);
    }

    [Fact]
    public void Pan_AndZoom_NeverRenderTheTerrainAgain()
    {
        var cut = RenderBoard(Start());
        var terrain = cut.FindComponent<BoardTerrain>();
        var renders = terrain.RenderCount;

        Press(cut, 100, 100);
        cut.Find("svg").PointerMove(new PointerEventArgs { ClientX = 160, ClientY = 100, Buttons = 1, IsPrimary = true });
        cut.Find("button.zoom-in").Click();

        terrain.RenderCount.ShouldBe(renders);
    }

    [Fact]
    public void Render_NewView_RendersTheTerrainAgain()
    {
        var cut = RenderBoard(Start());
        var terrain = cut.FindComponent<BoardTerrain>();
        var renders = terrain.RenderCount;

        cut.Render(parameters => parameters.Add(static board => board.View, Start()));

        terrain.RenderCount.ShouldBe(renders + 1);
    }

    [Fact]
    public void Wheel_TrackpadDelta_ZoomsALittle()
    {
        var cut = RenderBoard(Start());
        var fitted = Transform(cut).Scale;

        cut.Find("svg").Wheel(new WheelEventArgs { DeltaY = -10, ClientX = 400, ClientY = 300 });

        cut.WaitForAssertion(() => Transform(cut).Scale.ShouldBe(fitted * Math.Pow(GameBoard.ZoomStep, 0.1), 0.001));
    }

    [Fact]
    public void Move_WithoutAPress_DoesNotPan()
    {
        var cut = RenderBoard(Start());
        var before = Transform(cut);

        cut.Find("svg").PointerMove(new PointerEventArgs { ClientX = 300, ClientY = 300, Buttons = 0 });

        Transform(cut).ShouldBe(before);
    }

    [Fact]
    public void Wheel_UpThenDown_ZoomsInThenOut()
    {
        var cut = RenderBoard(Start());
        var fitted = Transform(cut).Scale;

        cut.Find("svg").Wheel(new WheelEventArgs { DeltaY = -100, ClientX = 400, ClientY = 300 });
        cut.WaitForAssertion(() => Transform(cut).Scale.ShouldBe(fitted * GameBoard.ZoomStep, 0.001));

        cut.Find("svg").Wheel(new WheelEventArgs { DeltaY = 100, ClientX = 400, ClientY = 300 });
        cut.WaitForAssertion(() => Transform(cut).Scale.ShouldBe(fitted, 0.001));
    }

    [Fact]
    public void ZoomButtons_ChangeTheScaleAndFitRestoresIt()
    {
        var cut = RenderBoard(Start());
        var fitted = Transform(cut);

        cut.Find("button.zoom-in").Click();
        Transform(cut).Scale.ShouldBe(fitted.Scale * GameBoard.ZoomStep, 0.001);
        cut.Find("button.zoom-out").Click();
        cut.Find("button.zoom-out").Click();
        Transform(cut).Scale.ShouldBe(fitted.Scale / GameBoard.ZoomStep, 0.001);

        cut.Find("button.zoom-fit").Click();
        cut.WaitForAssertion(() => Transform(cut).ShouldBe(fitted));
    }

    [Fact]
    public void Render_FirstTime_AddsTheWheelListenerAndFitsTheMeasuredBoard()
    {
        var cut = RenderBoard(Start());

        cut.WaitForAssertion(() => JSInterop.Invocations.Count(static invocation => invocation.Identifier == "measure").ShouldBe(1));
        JSInterop.Invocations.Count(static invocation => invocation.Identifier == "init").ShouldBe(1);
        Transform(cut).Scale.ShouldNotBe(1);
    }

    private IRenderedComponent<GameBoard> RenderBoard(
        PlayerView view, BoardSelection? selection = null, BoardHighlights? highlights = null, bool disabled = false, IMapLens? lens = null)
    {
        var cut = Render<GameBoard>(parameters => parameters
            .Add(static board => board.View, view)
            .Add(static board => board.Lens, lens)
            .Add(static board => board.Selection, selection ?? BoardSelection.None)
            .Add(static board => board.Highlights, highlights ?? BoardHighlights.None)
            .Add(static board => board.Disabled, disabled)
            .Add(static board => board.TileClicked, coord => _clicked.Add(coord)));
        cut.WaitForAssertion(() => JSInterop.Invocations.Count(static invocation => invocation.Identifier == "measure").ShouldBe(1));
        return cut;
    }

    private static IElement Tile(IRenderedComponent<GameBoard> cut, HexCoord coord) =>
        cut.Find($"use.tile[data-q='{coord.Q}'][data-r='{coord.R}']");

    private static void Press(IRenderedComponent<GameBoard> cut, double x, double y) =>
        cut.Find("svg").PointerDown(new PointerEventArgs { ClientX = x, ClientY = y, Buttons = 1, IsPrimary = true });

    private static (double X, double Y, double Scale) Transform(IRenderedComponent<GameBoard> cut)
    {
        var match = TransformRegex().Match(cut.Find("g.layer").GetAttribute("transform")!);
        return (Parse(match.Groups["x"].Value), Parse(match.Groups["y"].Value), Parse(match.Groups["scale"].Value));
    }

    private static double Parse(string value) => double.Parse(value, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^translate\((?<x>-?[\d.]+) (?<y>-?[\d.]+)\) scale\((?<scale>[\d.]+)\)$")]
    private static partial Regex TransformRegex();
}
