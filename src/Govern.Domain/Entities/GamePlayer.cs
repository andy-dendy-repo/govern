using Govern.Core.State;
using Govern.Domain.EntityBase;

namespace Govern.Domain.Entities;

/// <summary>
/// A user's seat in a <see cref="Game"/>. Its <see cref="BaseEntity.Id"/> doubles as the engine's <see cref="Core.State.PlayerId"/>,
/// so game states and player views never contain user ids. Created only through <see cref="Game"/>.
/// </summary>
public sealed class GamePlayer : BaseEntity
{
    private GamePlayer()
    {
    }

    /// <summary>
    /// The game this seat belongs to.
    /// </summary>
    public Guid GameId { get; private set; }

    /// <summary>
    /// The user in this seat; never sent to other players.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Zero-based seat, which is also the turn order: seat 0 (the creator) moves first.
    /// </summary>
    public int Seat { get; private set; }

    /// <summary>
    /// When the user took the seat.
    /// </summary>
    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>
    /// Whether the player has left a running game (resigned or eliminated) while it goes on for the others; copied from the
    /// state so lists and the seat limits never read the snapshot. Stays <see langword="false"/> for the last player standing.
    /// </summary>
    public bool IsOut { get; private set; }

    /// <summary>
    /// In a simultaneous game (<see cref="TurnMode.Simultaneous"/>), whether the player has ended this round's turn, which every
    /// player sees; copied from the state so the game list can show whose action a game waits for. Always
    /// <see langword="false"/> in a sequential game.
    /// </summary>
    public bool EndedTurn { get; private set; }

    /// <summary>
    /// The engine's identifier for this player: the per-game <see cref="BaseEntity.Id"/>, not the user id.
    /// </summary>
    public PlayerId PlayerId => new(Id);

    /// <summary>
    /// How many <see cref="GameLogEntry"/> rows this seat has; the next entry's <see cref="GameLogEntry.Sequence"/> is one more.
    /// </summary>
    public int LogLength { get; private set; }

    // Copies what every player sees of the seat from the state: whether it is out and whether it ended a simultaneous turn.
    // Returns whether anything changed.
    internal bool Sync(GameState state) => MarkOut(state) | SyncEndedTurn(state);

    // Marks the seat out once its player resigned or was eliminated (which every player sees) while the game goes on for the
    // others; a finished game ends every seat alike. Returns whether it changed.
    private bool MarkOut(GameState state)
    {
        if (IsOut || state.Status != GameStatus.InProgress || state.FindPlayer(PlayerId) is { Status: PlayerStatus.Active })
        {
            return false;
        }

        IsOut = true;
        return true;
    }

    // Copies the player's ended-turn flag from the state. Returns whether it changed.
    private bool SyncEndedTurn(GameState state)
    {
        var ended = state.FindPlayer(PlayerId) is { EndedTurn: true };
        if (ended == EndedTurn)
        {
            return false;
        }

        EndedTurn = ended;
        return true;
    }

    // Numbers the next log entry for this seat.
    internal int NextLogSequence() => ++LogLength;

    internal static GamePlayer Create(Guid gameId, Guid userId, int seat, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("The user id must not be empty.", nameof(userId));
        }

        return new GamePlayer { GameId = gameId, UserId = userId, Seat = seat, JoinedAt = now };
    }
}
