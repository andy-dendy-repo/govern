using Govern.Client.Features.Games.Enums;
using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Models;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Units;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Services;

/// <summary>
/// Plays the game log's new events on the board, one after another: a unit walks its path, an attacker lunges and damage
/// floats up, a destroyed unit fades, a new city, improvement, bridge or building (on its tile) pulses. The view is applied at once; only <see cref="Motion"/>
/// (where units are drawn, ghosts and effects) changes while it plays, so the panels and buttons never wait. Each log entry
/// plays once (by sequence), the first log seen on opening the game plays nothing, and a view with newer entries or <see cref="Finish"/>
/// ends what is playing (the same view arriving twice, from a command's answer and the stream, does not). <c>GameTable</c> creates one per game.
/// </summary>
/// <param name="time">The clock the steps wait on.</param>
public sealed class BoardAnimationService(TimeProvider time) : IDisposable
{
    /// <summary>How long a unit takes per tile.</summary>
    public static readonly TimeSpan Step = TimeSpan.FromMilliseconds(120);

    /// <summary>How long an attacker's lunge takes there and back.</summary>
    public static readonly TimeSpan Lunge = TimeSpan.FromMilliseconds(160);

    /// <summary>How long effects and fading ghosts stay (the CSS animations last as long).</summary>
    public static readonly TimeSpan EffectLifetime = TimeSpan.FromMilliseconds(900);

    private CancellationTokenSource? _playing;
    private int? _lastSequence;
    private int _nextKey;

    /// <summary>What the board draws differently right now; <see cref="BoardMotion.None"/> when nothing plays.</summary>
    public BoardMotion Motion { get; private set; } = BoardMotion.None;

    /// <summary>
    /// Whether the player asked for reduced motion (the browser's <c>prefers-reduced-motion</c>): units don't walk or lunge,
    /// and effects show without moving.
    /// </summary>
    public bool ReducedMotion { get; set; }

    /// <summary>Whether animations are playing.</summary>
    public bool IsPlaying => _playing is not null;

    /// <summary>Raised after <see cref="Motion"/> changed.</summary>
    public event Action? Changed;

    /// <summary>
    /// Plays the entries of <paramref name="log"/> not played yet, after ending what is playing; with none, what plays goes on. The first call only
    /// remembers where the log is, so opening a game replays nothing.
    /// </summary>
    /// <param name="previous">The view shown before (where moving units start); <see langword="null"/> when there was none.</param>
    /// <param name="current">The new view.</param>
    /// <param name="log">The player's recent log from the same response, oldest first.</param>
    /// <returns>A task that completes when the new entries have played (or were cut short).</returns>
    public Task PlayAsync(PlayerView? previous, PlayerView current, IReadOnlyList<GameLogItem>? log)
    {
        ArgumentNullException.ThrowIfNull(current);
        var entries = log ?? [];
        var last = _lastSequence;
        _lastSequence = Math.Max(last ?? 0, entries.Count == 0 ? 0 : entries.Max(static item => item.Sequence));
        var events = last is { } played
            ? entries.Where(item => item.Sequence > played).OrderBy(static item => item.Sequence).SelectMany(static item => item.Events).ToList()
            : [];

        // The same view arrives twice (the command's answer and the stream's snapshot): nothing new keeps what is playing.
        if (events.Count == 0)
        {
            return Task.CompletedTask;
        }

        Finish();
        var playing = _playing = new CancellationTokenSource();
        return RunAsync(new Scene(previous, current), events, playing);
    }

    /// <summary>Ends what is playing at once: every unit is drawn on its tile, ghosts and effects go.</summary>
    public void Finish()
    {
        if (_playing is { } playing)
        {
            _playing = null;
            playing.Cancel();
            playing.Dispose();
        }

        Show(BoardMotion.None);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Changed = null;
        Finish();
    }

    private async Task RunAsync(Scene scene, List<GameEvent> events, CancellationTokenSource playing)
    {
        try
        {
            foreach (var gameEvent in events)
            {
                await PlayAsync(scene, gameEvent, playing.Token);
            }

            if (Motion.Effects.Count > 0 || Motion.Ghosts.Count > 0)
            {
                await Task.Delay(EffectLifetime, time, playing.Token);
            }
        }
        catch (OperationCanceledException) when (playing.IsCancellationRequested)
        {
            // A newer view or a click ended it; Finish already cleared the board.
            return;
        }

        if (ReferenceEquals(_playing, playing))
        {
            Finish();
        }
    }

    private Task PlayAsync(Scene scene, GameEvent gameEvent, CancellationToken cancellationToken) => gameEvent switch
    {
        UnitMovedEvent moved when !ReducedMotion => WalkAsync(scene, moved, cancellationToken),
        UnitAttackedEvent attacked => AttackAsync(attacked, cancellationToken),
        UnitDestroyedEvent destroyed => Fade(destroyed),
        CityFoundedEvent founded => Pulse(founded.Position),
        CityCapturedEvent captured => Pulse(captured.Position),
        ImprovementBuiltEvent built => Pulse(built.Position),
        BuildingCompletedEvent completed => Pulse(completed.Position),
        BridgeBuiltEvent bridge => Pulse(bridge.From),
        _ => Task.CompletedTask,
    };

    // The unit steps tile by tile from where the previous view had it; one that is no longer in the view walks as a ghost.
    private async Task WalkAsync(Scene scene, UnitMovedEvent moved, CancellationToken cancellationToken)
    {
        List<HexCoord> tiles = [.. scene.Start(moved.UnitId) is { } start ? [start] : Array.Empty<HexCoord>(), .. moved.Path];
        scene.Moved(moved.UnitId, tiles[^1]);
        var ghost = scene.InCurrent(moved.UnitId) ? null : new BoardGhost(++_nextKey, moved.Owner, moved.UnitType, Point(tiles[0]), Fading: false);
        foreach (var tile in tiles)
        {
            Show(ghost is null
                ? Motion with { Positions = Motion.Positions.SetItem(moved.UnitId, Point(tile)) }
                : Motion with { Ghosts = Motion.Ghosts.RemoveAll(g => g.Key == ghost.Key).Add(ghost = ghost with { At = Point(tile) }) });
            await Task.Delay(Step, time, cancellationToken);
        }

        Show(ghost is null
            ? Motion with { Positions = Motion.Positions.Remove(moved.UnitId) }
            : Motion with { Ghosts = Motion.Ghosts.RemoveAll(g => g.Key == ghost.Key) });
    }

    // The attacker moves a third of the way to its target and back, then both damage numbers float up.
    private async Task AttackAsync(UnitAttackedEvent attacked, CancellationToken cancellationToken)
    {
        var target = Point(attacked.Target);
        if (!ReducedMotion && attacked is { AttackerId: { } attacker, From: { } fromTile })
        {
            var from = Point(fromTile);
            Show(Motion with { Positions = Motion.Positions.SetItem(attacker, new BoardPoint(from.X + ((target.X - from.X) / 3), from.Y + ((target.Y - from.Y) / 3))) });
            await Task.Delay(Lunge / 2, time, cancellationToken);
            Show(Motion with { Positions = Motion.Positions.Remove(attacker) });
            await Task.Delay(Lunge / 2, time, cancellationToken);
        }

        var effects = Motion.Effects.Add(new BoardEffect(++_nextKey, BoardEffectKind.Damage, target, $"-{attacked.DamageDealt}"));
        if (attacked is { DamageTaken: > 0, From: { } attackerTile })
        {
            effects = effects.Add(new BoardEffect(++_nextKey, BoardEffectKind.Damage, Point(attackerTile), $"-{attacked.DamageTaken}"));
        }

        Show(Motion with { Effects = effects });
    }

    private Task Fade(UnitDestroyedEvent destroyed)
    {
        Show(Motion with { Ghosts = Motion.Ghosts.Add(new BoardGhost(++_nextKey, destroyed.Owner, destroyed.UnitType, Point(destroyed.Position), Fading: true)) });
        return Task.CompletedTask;
    }

    private Task Pulse(HexCoord tile)
    {
        Show(Motion with { Effects = Motion.Effects.Add(new BoardEffect(++_nextKey, BoardEffectKind.Pulse, Point(tile))) });
        return Task.CompletedTask;
    }

    private void Show(BoardMotion motion)
    {
        if (ReferenceEquals(motion, Motion))
        {
            return;
        }

        Motion = motion;
        Changed?.Invoke();
    }

    private static BoardPoint Point(HexCoord tile)
    {
        var (x, y) = HexLayout.Center(tile);
        return new BoardPoint(x, y);
    }

    // The views before and after, for where a unit starts (where its last walk in this batch ended, else its tile before) and
    // whether it is still seen.
    private sealed class Scene(PlayerView? previous, PlayerView current)
    {
        private readonly Dictionary<UnitId, HexCoord> _walked = [];

        public HexCoord? Start(UnitId unit) =>
            _walked.TryGetValue(unit, out var tile) ? tile : previous?.Units.FirstOrDefault(candidate => candidate.Id == unit)?.Position;

        public void Moved(UnitId unit, HexCoord tile) => _walked[unit] = tile;

        public bool InCurrent(UnitId unit) => current.Units.Any(candidate => candidate.Id == unit);
    }
}
