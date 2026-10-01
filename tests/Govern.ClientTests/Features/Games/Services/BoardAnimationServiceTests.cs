using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Client.Features.Games.Services;
using Govern.ClientTests.Support;
using Govern.Core.Cities;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Units;
using Govern.Core.Visibility;
using Microsoft.Extensions.Time.Testing;
using static Govern.ClientTests.Support.TestViews;

namespace Govern.ClientTests.Features.Games.Services;

public sealed class BoardAnimationServiceTests : IDisposable
{
    private static readonly UnitId Mover = new(1);
    private static readonly PlayerView Before = Create(units: [Soldier(1, You, At(2, 2)), Soldier(2, Opponent, At(5, 2))]);
    private static readonly PlayerView After = Create(units: [Soldier(1, You, At(4, 2)), Soldier(2, Opponent, At(5, 2))]);

    private readonly FakeTimeProvider _time = new();
    private readonly BoardAnimationService _animation;

    public BoardAnimationServiceTests() => _animation = new BoardAnimationService(_time);

    public void Dispose() => _animation.Dispose();

    [Fact]
    public async Task PlayAsync_FirstLog_PlaysNothing()
    {
        await _animation.PlayAsync(null, After, [Entry(1, Moved())]);

        _animation.IsPlaying.ShouldBeFalse();
        _animation.Motion.ShouldBe(BoardMotion.None);
    }

    [Fact]
    public async Task PlayAsync_NewMove_WalksTheUnitTileByTileThenLeavesItOnItsTile()
    {
        await _animation.PlayAsync(null, Before, []);

        var playing = _animation.PlayAsync(Before, After, [Entry(1, Moved())]);

        _animation.Motion.Positions[Mover].ShouldBe(Point(At(2, 2)));
        _time.Advance(BoardAnimationService.Step);
        await Eventually.HoldsAsync(() => _animation.Motion.Positions.GetValueOrDefault(Mover) == Point(At(3, 2)), "the unit takes its first step");
        _time.Advance(BoardAnimationService.Step);
        await Eventually.HoldsAsync(() => _animation.Motion.Positions.GetValueOrDefault(Mover) == Point(At(4, 2)), "the unit reaches its tile");
        _time.Advance(BoardAnimationService.Step);
        await playing;
        _animation.Motion.ShouldBe(BoardMotion.None);
        _animation.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public async Task PlayAsync_SameLogAgain_PlaysEachEntryOnce()
    {
        await _animation.PlayAsync(null, Before, [Entry(1, Moved())]);

        await _animation.PlayAsync(Before, After, [Entry(1, Moved())]);

        _animation.IsPlaying.ShouldBeFalse();
        _animation.Motion.ShouldBe(BoardMotion.None);
    }

    [Fact]
    public async Task PlayAsync_SameViewAgainWhilePlaying_KeepsPlaying()
    {
        await _animation.PlayAsync(null, Before, []);
        _ = _animation.PlayAsync(Before, After, [Entry(1, Moved())]);

        await _animation.PlayAsync(After, After, [Entry(1, Moved())]);

        _animation.IsPlaying.ShouldBeTrue();
        _animation.Motion.Positions[Mover].ShouldBe(Point(At(2, 2)));
    }

    [Fact]
    public async Task PlayAsync_TwoMovesOfOneUnitInOneView_StartsTheSecondWhereTheFirstEnded()
    {
        await _animation.PlayAsync(null, Before, []);
        var further = Create(units: [Soldier(1, You, At(5, 3)), Soldier(2, Opponent, At(5, 2))]);
        var second = new UnitMovedEvent(Mover, You, SoldierType, [At(5, 3)]);

        var drawn = new List<BoardPoint?>();
        _animation.Changed += () => drawn.Add(_animation.Motion.Positions.TryGetValue(Mover, out var at) ? at : null);

        _ = _animation.PlayAsync(Before, further, [Entry(1, Moved()), Entry(2, second)]);
        for (var step = 1; step <= 3; step++)
        {
            var before = drawn.Count;
            _time.Advance(BoardAnimationService.Step);
            await Eventually.HoldsAsync(() => drawn.Count > before, "the next step is drawn");
        }

        await Eventually.HoldsAsync(() => drawn.Count == 5, "the first walk ends and the second begins");
        drawn.ShouldBe([Point(At(2, 2)), Point(At(3, 2)), Point(At(4, 2)), null, Point(At(4, 2))]);
    }

    [Fact]
    public async Task Finish_WhilePlaying_PutsEveryUnitOnItsTileAtOnce()
    {
        await _animation.PlayAsync(null, Before, []);
        var changes = 0;
        _animation.Changed += () => changes++;
        var playing = _animation.PlayAsync(Before, After, [Entry(1, Moved())]);

        _animation.Finish();

        await playing;
        _animation.Motion.ShouldBe(BoardMotion.None);
        changes.ShouldBe(2);
    }

    [Fact]
    public async Task PlayAsync_NewerViewWhilePlaying_EndsTheRunningAnimation()
    {
        await _animation.PlayAsync(null, Before, []);
        var first = _animation.PlayAsync(Before, After, [Entry(1, Moved())]);

        var second = _animation.PlayAsync(After, After, [Entry(1, Moved()), Entry(2, new UnitDestroyedEvent(new UnitId(2), Opponent, SoldierType, At(5, 2)))]);

        await first;
        _animation.Motion.Positions.ShouldBeEmpty();
        _animation.Motion.Ghosts.ShouldHaveSingleItem().Fading.ShouldBeTrue();
        _time.Advance(BoardAnimationService.EffectLifetime);
        await second;
        _animation.Motion.ShouldBe(BoardMotion.None);
    }

    [Fact]
    public async Task PlayAsync_ReducedMotion_SkipsMovesButShowsDamage()
    {
        _animation.ReducedMotion = true;
        await _animation.PlayAsync(null, Before, []);
        var attack = new UnitAttackedEvent(Mover, You, SoldierType, At(4, 2), new UnitId(2), Opponent, SoldierType, At(5, 2), 22, 6);

        _ = _animation.PlayAsync(Before, After, [Entry(1, Moved(), attack)]);

        _animation.Motion.Positions.ShouldBeEmpty();
        _animation.Motion.Effects.Select(static effect => (effect.Kind, effect.Text)).ShouldBe(
            [(BoardEffectKind.Damage, "-22"), (BoardEffectKind.Damage, "-6")]);
        _animation.Motion.Effects[0].At.ShouldBe(Point(At(5, 2)));
    }

    [Fact]
    public async Task PlayAsync_Attack_LungesTheAttackerThenFloatsTheDamage()
    {
        await _animation.PlayAsync(null, After, []);
        var attack = new UnitAttackedEvent(Mover, You, SoldierType, At(4, 2), new UnitId(2), Opponent, SoldierType, At(5, 2), 22, 0);

        var playing = _animation.PlayAsync(After, After, [Entry(1, attack)]);

        var lunge = _animation.Motion.Positions[Mover];
        lunge.X.ShouldBeGreaterThan(Point(At(4, 2)).X);
        lunge.X.ShouldBeLessThan(Point(At(5, 2)).X);
        _time.Advance(BoardAnimationService.Lunge / 2);
        await Eventually.HoldsAsync(() => _animation.Motion.Positions.IsEmpty, "the attacker steps back");
        _time.Advance(BoardAnimationService.Lunge / 2);
        await Eventually.HoldsAsync(() => _animation.Motion.Effects.Count == 1, "the damage floats up");
        _animation.Motion.Effects[0].Text.ShouldBe("-22");
        _time.Advance(BoardAnimationService.EffectLifetime);
        await playing;
        _animation.Motion.ShouldBe(BoardMotion.None);
    }

    [Fact]
    public async Task PlayAsync_UnitThatLeftTheView_WalksAsAGhost()
    {
        var gone = Create(units: [Soldier(1, You, At(2, 2))]);
        await _animation.PlayAsync(null, Before, []);
        var moved = new UnitMovedEvent(new UnitId(2), Opponent, SoldierType, [At(6, 2)]);

        _ = _animation.PlayAsync(Before, gone, [Entry(1, moved)]);

        _animation.Motion.Positions.ShouldBeEmpty();
        var ghost = _animation.Motion.Ghosts.ShouldHaveSingleItem();
        (ghost.Owner, ghost.Type, ghost.At, ghost.Fading).ShouldBe((Opponent, SoldierType, Point(At(5, 2)), false));
    }

    [Fact]
    public async Task PlayAsync_CityFounded_PulsesItsTile()
    {
        await _animation.PlayAsync(null, Before, []);

        _ = _animation.PlayAsync(Before, After, [Entry(1, new CityFoundedEvent(new CityId(9), You, At(3, 3), "City 1", Mover))]);

        var pulse = _animation.Motion.Effects.ShouldHaveSingleItem();
        (pulse.Kind, pulse.At).ShouldBe((BoardEffectKind.Pulse, Point(At(3, 3))));
    }

    private static UnitMovedEvent Moved() => new(Mover, You, SoldierType, [At(3, 2), At(4, 2)]);

    private static GameLogItem Entry(int sequence, params GameEvent[] events) => new(sequence, 1, [.. events]);

    private static BoardPoint Point(HexCoord tile)
    {
        var (x, y) = HexLayout.Center(tile);
        return new BoardPoint(x, y);
    }
}
