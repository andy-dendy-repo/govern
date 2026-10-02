using System.Collections.Immutable;
using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.Core.Map;
using Govern.Core.Units;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Helpers;

public class SceneMotionBuilderTests
{
    [Fact]
    public void Build_Position_LandsOnTheSceneCentreOfItsTileWithItsSurface()
    {
        var view = Create([Soldier(2, You, At(3, 2))], terrain: static coord => coord == At(4, 2) ? Hills : Plains);
        var motion = BoardMotion.None with { Positions = ImmutableDictionary<UnitId, BoardPoint>.Empty.Add(new UnitId(2), Point(At(4, 2))) };

        var position = SceneMotionBuilder.Build(motion, view).Positions.ShouldHaveSingleItem();

        var (x, z) = BoardSceneBuilder.Center(At(4, 2));
        (position.Unit, position.X, position.Z).ShouldBe((2, x, z));
        position.Height.ShouldBe(TerrainReliefs.Surface(view.TileAt(At(4, 2))));
        position.Height.ShouldBeGreaterThan(TerrainReliefs.Surface(view.TileAt(At(3, 2))));
    }

    [Fact]
    public void Build_PointBetweenTiles_TakesTheSurfaceOfTheNearerTile()
    {
        var view = Create(terrain: static coord => coord == At(3, 2) ? Hills : Plains);
        var (from, to) = (Point(At(3, 2)), Point(At(4, 2)));
        var lunge = new BoardPoint(from.X + ((to.X - from.X) / 3), from.Y + ((to.Y - from.Y) / 3));
        var motion = BoardMotion.None with { Positions = ImmutableDictionary<UnitId, BoardPoint>.Empty.Add(new UnitId(2), lunge) };

        SceneMotionBuilder.Build(motion, view).Positions.ShouldHaveSingleItem().Height.ShouldBe(TerrainReliefs.Surface(view.TileAt(At(3, 2))));
    }

    [Fact]
    public void Build_Ghost_KeepsItsModelSeatAndFading()
    {
        var motion = BoardMotion.None with { Ghosts = [new BoardGhost(5, Opponent, HorsemanType, Point(At(4, 2)), Fading: true)] };

        var ghost = SceneMotionBuilder.Build(motion, Start()).Ghosts.ShouldHaveSingleItem();

        (ghost.Key, ghost.Fading).ShouldBe((5, true));
        (ghost.Piece.Model, ghost.Piece.Seat, ghost.Piece.Own, ghost.Piece.Icon, ghost.Piece.Unit).ShouldBe(("horseman", 2, false, "unit-horseman", (int?)null));
        (ghost.Piece.Q, ghost.Piece.R).ShouldBe((At(4, 2).Q, At(4, 2).R));
    }

    [Fact]
    public void Build_Effects_CarryTheirKeyKindAndText()
    {
        var motion = BoardMotion.None with
        {
            Effects = [new BoardEffect(7, BoardEffectKind.Damage, Point(At(3, 2)), "-12"), new BoardEffect(8, BoardEffectKind.Pulse, Point(At(1, 1)))],
        };

        var effects = SceneMotionBuilder.Build(motion, Start()).Effects;

        effects.Select(static effect => (effect.Key, effect.Kind, effect.Text)).ShouldBe([(7, "damage", "-12"), (8, "pulse", null)]);
        var (x, z) = BoardSceneBuilder.Center(At(1, 1));
        (effects[1].X, effects[1].Z).ShouldBe((x, z));
    }

    [Fact]
    public void Build_None_IsEmptyWithTheServicesDurations()
    {
        var motion = SceneMotionBuilder.Build(BoardMotion.None, Start());

        (motion.Positions.Count, motion.Ghosts.Count, motion.Effects.Count).ShouldBe((0, 0, 0));
        (motion.Step, motion.Lunge, motion.Lifetime).ShouldBe((
            BoardAnimationService.Step.TotalMilliseconds, BoardAnimationService.Lunge.TotalMilliseconds, BoardAnimationService.EffectLifetime.TotalMilliseconds));
    }

    [Fact]
    public void Build_NoViewYet_DrawsNothing()
    {
        var motion = BoardMotion.None with { Effects = [new BoardEffect(1, BoardEffectKind.Pulse, Point(At(1, 1)))] };

        SceneMotionBuilder.Build(motion, null).Effects.ShouldBeEmpty();
    }

    private static BoardPoint Point(HexCoord tile)
    {
        var (x, y) = HexLayout.Center(tile);
        return new BoardPoint(x, y);
    }
}
