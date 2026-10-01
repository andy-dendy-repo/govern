using System.Collections.Immutable;
using Govern.Core.Events;
using Govern.Domain.EntityBase;

namespace Govern.Domain.Entities;

/// <summary>
/// What one player saw of one accepted command: the command's events filtered for that seat
/// (<see cref="Core.Visibility.VisibilityCalculator.EventsFor"/>). Written by <see cref="Game.Execute"/> only when the
/// player saw something. It carries no game version or time, since those would count the opponent's hidden commands.
/// </summary>
public sealed class GameLogEntry : BaseEntity
{
    private GameLogEntry()
    {
    }

    /// <summary>
    /// The game.
    /// </summary>
    public Guid GameId { get; private set; }

    /// <summary>
    /// The seat (<see cref="GamePlayer"/> id) this entry was filtered for.
    /// </summary>
    public Guid PlayerId { get; private set; }

    /// <summary>
    /// 1-based position in this seat's log; it counts only the entries this seat received.
    /// </summary>
    public int Sequence { get; private set; }

    /// <summary>
    /// The round (<see cref="Core.State.GameState.Turn"/>) the command was sent in.
    /// </summary>
    public int Turn { get; private set; }

    /// <summary>
    /// The events the seat may see, in order; never empty.
    /// </summary>
    public ImmutableArray<GameEvent> Events { get; private set; } = [];

    internal static GameLogEntry Create(Guid gameId, Guid playerId, int sequence, int turn, ImmutableArray<GameEvent> events)
    {
        if (events.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A log entry needs at least one event.", nameof(events));
        }

        return new GameLogEntry { GameId = gameId, PlayerId = playerId, Sequence = sequence, Turn = turn, Events = events };
    }
}
