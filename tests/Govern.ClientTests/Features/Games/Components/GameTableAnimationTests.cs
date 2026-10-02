using Govern.Client.Features.Games.Components;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Units;
using Govern.Core.Visibility;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Components;

/// <summary>The board plays the new log entries of a newer view (BoardAnimationService).</summary>
public class GameTableAnimationTests : ClientTestContext
{
    private static readonly PlayerView Before = Create(units: [Soldier(1, You, At(2, 2)), Soldier(2, Opponent, At(4, 2))]);
    private static readonly PlayerView After = Create(units: [Soldier(1, You, At(3, 2)), Soldier(2, Opponent, At(4, 2))]);

    public GameTableAnimationTests() => SignIn();

    [Fact]
    public void OpeningTheGame_DrawsUnitsOnTheirTilesWithoutPlayingTheLog()
    {
        var cut = RenderTable(After, [new(1, 1, [Moved()])]);

        UnitStyle(cut).ShouldBe(StyleAt(At(3, 2)));
        cut.FindAll(".effect").ShouldBeEmpty();
    }

    [Fact]
    public void NewerView_WalksTheMovedUnitFromWhereItWas()
    {
        var cut = RenderTable(Before, []);

        Update(cut, After, [new(1, 1, [Moved()])]);

        UnitStyle(cut).ShouldBe(StyleAt(At(2, 2)));
        Time.Advance(BoardAnimationService.Step);
        cut.WaitForAssertion(() => UnitStyle(cut).ShouldBe(StyleAt(At(3, 2))));
    }

    [Fact]
    public void NewerView_WithAnAttack_FloatsTheDamageOnTheEffectsLayer()
    {
        var cut = RenderTable(After, []);
        var attack = new UnitAttackedEvent(new UnitId(1), You, SoldierType, At(3, 2), new UnitId(2), Opponent, SoldierType, At(4, 2), 22, 0);

        Update(cut, After with { Turn = 2 }, [new(1, 1, [attack])]);
        UnitStyle(cut).ShouldNotBe(StyleAt(At(3, 2)));
        Time.Advance(BoardAnimationService.Lunge / 2);
        cut.WaitForAssertion(() => UnitStyle(cut).ShouldBe(StyleAt(At(3, 2))));
        Time.Advance(BoardAnimationService.Lunge / 2);

        cut.WaitForAssertion(() => cut.Find("[data-layer=effects] .effect.damage").TextContent.Trim().ShouldBe("-22"));
    }

    [Fact]
    public void TileClick_WhilePlaying_FinishesTheAnimation()
    {
        var cut = RenderTable(Before, []);
        Update(cut, After, [new(1, 1, [Moved()])]);

        cut.FindAll("[data-layer=terrain] use")[0].Click();

        UnitStyle(cut).ShouldBe(StyleAt(At(3, 2)));
    }

    [Fact]
    public void NewerView_On3DBoard_SendsTheWalkToTheRenderer()
    {
        var module = JSInterop.SetupModule(Board3DInteropService.ModulePath);
        module.Setup<bool>("mount", _ => true).SetResult(true);
        foreach (var call in new[] { "render", "renderOverlay", "renderMotion", "fit", "focus", "unmount" })
        {
            module.SetupVoid(call, _ => true).SetVoidResult();
        }

        var cut = RenderTable(Before, []);
        cut.Find("button.board-mode").Click();

        Update(cut, After, [new(1, 1, [Moved()])]);

        var (x, z) = BoardSceneBuilder.Center(At(2, 2));
        var walk = module.Invocations["renderMotion"][^1].Arguments[1].ShouldBeOfType<SceneMotion>().Positions.ShouldHaveSingleItem();
        (walk.Unit, walk.X, walk.Z).ShouldBe((1, x, z));
    }

    private static UnitMovedEvent Moved() => new(new UnitId(1), You, SoldierType, [At(3, 2)]);

    private static string? UnitStyle(IRenderedComponent<GameTable> cut) => cut.Find("use.unit.own").ParentElement!.GetAttribute("style");

    private static string StyleAt(HexCoord tile)
    {
        var (x, y) = HexLayout.Center(tile);
        return $"transform: translate({HexLayout.Format(x)}px, {HexLayout.Format(y)}px)";
    }

    private IRenderedComponent<GameTable> RenderTable(PlayerView view, IReadOnlyList<GameLogItem> log) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view) with { Log = log }));

    private static void Update(IRenderedComponent<GameTable> cut, PlayerView view, IReadOnlyList<GameLogItem> log) =>
        cut.Render(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view) with { Log = log }));
}
