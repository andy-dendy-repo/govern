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
    // A newer view has a new version, which is what makes the board draw its scene again.
    private static readonly PlayerView After =
        Create(units: [Soldier(1, You, At(3, 2)), Soldier(2, Opponent, At(4, 2))]) with { Version = TestViews.Version + 1 };

    private readonly FakeBoardRenderer _board;

    public GameTableAnimationTests()
    {
        SignIn();
        _board = new FakeBoardRenderer(JSInterop);
    }

    [Fact]
    public void OpeningTheGame_DrawsUnitsOnTheirTilesWithoutPlayingTheLog()
    {
        var cut = RenderTable(After, [new(1, 1, [Moved()])]);

        UnitAt().ShouldBe(Center(At(3, 2)));
        _board.Motion.Effects.ShouldBeEmpty();
    }

    [Fact]
    public void NewerView_WalksTheMovedUnitFromWhereItWas()
    {
        var cut = RenderTable(Before, []);

        Update(cut, After, [new(1, 1, [Moved()])]);

        UnitAt().ShouldBe(Center(At(2, 2)));
        Time.Advance(BoardAnimationService.Step);
        cut.WaitForAssertion(() => UnitAt().ShouldBe(Center(At(3, 2))));
    }

    [Fact]
    public void NewerView_WithAnAttack_LungesAndFloatsTheDamage()
    {
        var cut = RenderTable(After, []);
        var attack = new UnitAttackedEvent(new UnitId(1), You, SoldierType, At(3, 2), new UnitId(2), Opponent, SoldierType, At(4, 2), 22, 0);

        Update(cut, After with { Turn = 2 }, [new(1, 1, [attack])]);
        UnitAt().ShouldNotBe(Center(At(3, 2)));
        Time.Advance(BoardAnimationService.Lunge / 2);
        cut.WaitForAssertion(() => UnitAt().ShouldBe(Center(At(3, 2))));
        Time.Advance(BoardAnimationService.Lunge / 2);

        cut.WaitForAssertion(() => _board.Motion.Effects.Where(static effect => effect.Kind == "damage")
            .Select(static effect => effect.Text).ShouldContain("-22"));
    }

    [Fact]
    public void NewerView_PlayerAskedForReducedMotion_ShowsTheUnitOnItsNewTileAtOnce()
    {
        JSInterop.SetupModule(GameScreenInteropService.ModulePath).Setup<bool>("prefersReducedMotion", _ => true).SetResult(true);
        var cut = RenderTable(Before, []);

        Update(cut, After, [new(1, 1, [Moved()])]);

        UnitAt().ShouldBe(Center(At(3, 2)));
    }

    [Fact]
    public void TileClick_WhilePlaying_FinishesTheAnimation()
    {
        var cut = RenderTable(Before, []);
        Update(cut, After, [new(1, 1, [Moved()])]);

        _board.Click(At(0, 0));

        cut.WaitForAssertion(() => UnitAt().ShouldBe(Center(At(3, 2))));
    }

    private static UnitMovedEvent Moved() => new(new UnitId(1), You, SoldierType, [At(3, 2)]);

    // Where the board draws unit 1: where the motion holds it, else on its tile in the scene. Rounded, as the motion's points
    // pass through the 2D layout first.
    private (double X, double Z) UnitAt() =>
        _board.Motion.Positions.Where(static position => position.Unit == 1).Select(static position => Round(position.X, position.Z))
            .Concat(_board.Scene.Pieces.Where(static piece => piece.Unit == 1).Select(static piece => Round(piece.X, piece.Z)))
            .First();

    private static (double X, double Z) Center(HexCoord tile)
    {
        var (x, z) = BoardSceneBuilder.Center(tile);
        return Round(x, z);
    }

    private static (double X, double Z) Round(double x, double z) => (Math.Round(x, 6), Math.Round(z, 6));

    private IRenderedComponent<GameTable> RenderTable(PlayerView view, IReadOnlyList<GameLogItem> log) =>
        Render<GameTable>(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view) with { Log = log }));

    private static void Update(IRenderedComponent<GameTable> cut, PlayerView view, IReadOnlyList<GameLogItem> log) =>
        cut.Render(parameters => parameters.Add(static table => table.Game, TestGames.Started(view: view) with { Log = log }));
}
