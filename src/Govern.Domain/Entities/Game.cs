using System.Buffers.Binary;
using System.Security.Cryptography;
using Govern.Core.Engine;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Visibility;
using Govern.Domain.EntityBase;
using Govern.Domain.Enums;
using Govern.Domain.Events;
using Govern.Domain.ValueObjects;

namespace Govern.Domain.Entities;

/// <summary>
/// A game of 2 to 4 players (<see cref="Seats"/>): a lobby joined through an invite code, then a running game whose whole <see cref="GameState"/> is
/// stored as one snapshot. The aggregate root for <see cref="GamePlayer"/>. Every change increments <see cref="Revision"/>,
/// which the persistence layer uses as an optimistic concurrency token, and raises a <see cref="GameChangedEvent"/>. A lobby
/// can be left by the joiner and cancelled by the creator; a running game ends through the engine (e.g. resign).
/// </summary>
/// <remarks>
/// The methods throw on calls the rules forbid (joining a full game, starting as a non-creator): handlers check those
/// conditions first and return errors, so a throw here is a programming error.
/// </remarks>
public sealed class Game : BaseAuditableEntity
{
    /// <summary>The fewest seats a game has; the ruleset's <see cref="SetupRules"/> decides the most, and handlers check both.</summary>
    public const int MinSeats = 2;

    /// <summary>How long a lobby may go without activity (<see cref="ActivityAt"/>) before it expires and is deleted.</summary>
    public static readonly TimeSpan LobbyLifetime = TimeSpan.FromDays(7);

    private readonly List<GamePlayer> _players = [];
    private readonly List<GameLogEntry> _logEntries = [];

    private Game()
    {
    }

    /// <summary>The user who created the game; only they may start it.</summary>
    public Guid CreatorId { get; private set; }

    /// <summary>
    /// The secret that lets another user join while the game is in the <see cref="GamePhase.Lobby"/>; unique across games.
    /// </summary>
    public InviteCode InviteCode { get; private set; } = null!;

    /// <summary>How many players the game is for: the lobby starts once every seat is taken.</summary>
    public int Seats { get; private set; } = MinSeats;

    /// <summary>The map the game is played on; <see cref="MapChoice.Random"/> only until the game starts.</summary>
    public MapChoice Map { get; private set; } = MapChoice.Generated;

    /// <summary>Where the game is in its lifecycle.</summary>
    public GamePhase Phase { get; private set; }

    /// <summary>Incremented by every change, so two concurrent changes to the same game cannot both be saved.</summary>
    public int Revision { get; private set; }

    /// <summary>
    /// The engine state; <see langword="null"/> while in the <see cref="GamePhase.Lobby"/>. Immutable: changes replace it.
    /// </summary>
    public GameState? State { get; private set; }

    /// <summary>
    /// The seat id (<see cref="GamePlayer.PlayerId"/>) of the player whose turn it is, copied from <see cref="State"/> so lists can show
    /// "your turn" without reading the snapshot; <see langword="null"/> in the lobby and once the game has finished.
    /// </summary>
    public Guid? CurrentPlayerId { get; private set; }

    /// <summary>
    /// When the game last changed in a way every player sees: created, a seat taken or left, started, the turn passed, a player
    /// left or the game finished. Unlike <see cref="BaseAuditableEntity.UpdatedAt"/>, a command whose effect may be hidden from
    /// others (a move, a production change) leaves it alone, so it reveals nothing about hidden activity.
    /// </summary>
    public DateTimeOffset ActivityAt { get; private set; }

    /// <summary>The seated players (the caller must load them).</summary>
    public IReadOnlyCollection<GamePlayer> Players => _players.AsReadOnly();

    /// <summary>
    /// The players' log entries. Never loaded with the game: it holds the entries <see cref="Execute"/> added since the game was
    /// loaded, which saving inserts. Read a player's log with a query on the entries.
    /// </summary>
    public IReadOnlyCollection<GameLogEntry> LogEntries => _logEntries.AsReadOnly();

    /// <summary>Whether every seat is taken.</summary>
    public bool IsFull => _players.Count >= Seats;

    /// <summary>Creates a lobby with a new invite code; the creator takes seat 0.</summary>
    /// <param name="creatorId">The creating user.</param>
    /// <param name="now">The current time.</param>
    /// <param name="map">
    /// The map choice (the handler checks that a key is in the pool, and passes <see cref="MapChoice.Random"/> when the request
    /// names none); <see langword="null"/> for a generated map.
    /// </param>
    /// <param name="seats">
    /// How many players the game is for, at least <see cref="MinSeats"/> (the handler checks the ruleset's maximum, and that a
    /// named pool map is made for this many).
    /// </param>
    /// <returns>The new game.</returns>
    /// <exception cref="ArgumentException"><paramref name="creatorId"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seats"/> is below <see cref="MinSeats"/>.</exception>
    public static Game Create(Guid creatorId, DateTimeOffset now, MapChoice? map = null, int seats = MinSeats)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(seats, MinSeats);
        var game = new Game
        {
            Seats = seats,
            CreatorId = creatorId,
            InviteCode = InviteCode.New(),
            Phase = GamePhase.Lobby,
            ActivityAt = now,
            Map = map ?? MapChoice.Generated,
        };
        game._players.Add(GamePlayer.Create(game.Id, creatorId, seat: 0, now));
        return game;
    }

    /// <summary>Returns the seat of <paramref name="userId"/>.</summary>
    /// <param name="userId">The user.</param>
    /// <returns>The player, or <see langword="null"/> when the user is not in this game.</returns>
    public GamePlayer? FindPlayer(Guid userId) => _players.Find(player => player.UserId == userId);

    /// <summary>Whether <paramref name="userId"/> has a seat in this game.</summary>
    /// <param name="userId">The user.</param>
    /// <returns><see langword="true"/> for a seated player.</returns>
    public bool IsPlayer(Guid userId) => FindPlayer(userId) is not null;

    /// <summary>
    /// Seats <paramref name="userId"/> in the lowest free seat. Joining again returns the existing seat and changes nothing.
    /// </summary>
    /// <param name="userId">The joining user.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The user's seat.</returns>
    /// <exception cref="InvalidOperationException">A new player joins a game that is not in the lobby or is full.</exception>
    public GamePlayer Join(Guid userId, DateTimeOffset now)
    {
        if (FindPlayer(userId) is { } existing)
        {
            return existing;
        }

        if (Phase != GamePhase.Lobby || IsFull)
        {
            throw new InvalidOperationException("Only a lobby with a free seat can be joined.");
        }

        var seat = Enumerable.Range(0, Seats).First(free => _players.TrueForAll(player => player.Seat != free));
        var player = GamePlayer.Create(Id, userId, seat, now);
        _players.Add(player);
        MarkChanged(now);
        return player;
    }

    /// <summary>
    /// Frees the seat of <paramref name="userId"/> in the lobby. The creator cannot leave; they <see cref="Cancel"/> instead.
    /// </summary>
    /// <param name="userId">The leaving user.</param>
    /// <param name="now">The current time.</param>
    /// <exception cref="InvalidOperationException">
    /// The game is not in the lobby, the user has no seat in it, or the user is the creator.
    /// </exception>
    public void Leave(Guid userId, DateTimeOffset now)
    {
        if (Phase != GamePhase.Lobby || userId == CreatorId || FindPlayer(userId) is not { } player)
        {
            throw new InvalidOperationException("Only a joined player can leave, and only a lobby.");
        }

        // The creator can't leave, so seat 0 is never freed; Join fills the lowest free seat again.
        _players.Remove(player);
        MarkChanged(now);
    }

    /// <summary>
    /// Cancels the lobby: raises the change for its watchers. The caller then deletes the game, which frees the seats and
    /// retires the invite code.
    /// </summary>
    /// <param name="userId">The user cancelling the lobby.</param>
    /// <exception cref="InvalidOperationException">The user is not the creator, or the game is not in the lobby.</exception>
    public void Cancel(Guid userId)
    {
        if (userId != CreatorId || Phase != GamePhase.Lobby)
        {
            throw new InvalidOperationException("Only the creator can cancel, and only a lobby.");
        }

        AddDomainEvent(new GameChangedEvent(Id));
    }

    /// <summary>
    /// Starts the game: creates the engine state from the ruleset, with the players in seat order and a secret random seed.
    /// </summary>
    /// <param name="userId">The user starting the game.</param>
    /// <param name="ruleset">The ruleset the game is played with.</param>
    /// <param name="now">The current time.</param>
    /// <param name="map">
    /// The pool map to play on, which must fit <see cref="Map"/> (<see cref="MapChoice.Fits"/>) and be made for
    /// <see cref="Seats"/> players; <see langword="null"/> for a generated map.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The user is not the creator, the game is not in the lobby, it is not full, or the map doesn't fit the lobby's choice or
    /// its seats.
    /// </exception>
    public void Start(Guid userId, Ruleset ruleset, DateTimeOffset now, PredefinedMap? map = null)
    {
        ArgumentNullException.ThrowIfNull(ruleset);

        if (userId != CreatorId || Phase != GamePhase.Lobby || !IsFull || !Map.Fits(map) || (map is not null && map.Players != Seats))
        {
            throw new InvalidOperationException("Only the creator can start a full lobby, on the map it chose.");
        }

        var players = _players.OrderBy(static player => player.Seat).Select(static player => player.PlayerId);
        State = GameFactory.Create(ruleset, new GameSetup([.. players], NewSeed(), Map: map));
        Map = MapChoice.Of(map);
        Phase = GamePhase.InProgress;
        CurrentPlayerId = State.CurrentPlayer.Value;
        MarkChanged(now);
    }

    /// <summary>
    /// Runs one player's command through the engine. An accepted command replaces <see cref="State"/>, moves
    /// <see cref="CurrentPlayerId"/>, marks the seats of players who left (<see cref="GamePlayer.IsOut"/>) and finishes the game
    /// when the engine does, and adds a <see cref="GameLogEntry"/> for each
    /// seat that saw any of its events; a rejected one changes nothing. <see cref="ActivityAt"/> moves only when the turn passes or the game finishes.
    /// </summary>
    /// <param name="userId">The seated user sending the command.</param>
    /// <param name="ruleset">The ruleset the game was started with.</param>
    /// <param name="command">The command.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The engine's result: the update, or the <see cref="RuleViolation"/> that rejected the command.</returns>
    /// <exception cref="InvalidOperationException">The game has not started, or the user has no seat in it.</exception>
    public CommandResult Execute(Guid userId, Ruleset ruleset, GameCommand command, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(command);

        if (State is null || FindPlayer(userId) is not { } player)
        {
            throw new InvalidOperationException("Only a seated player of a started game can send commands.");
        }

        var before = State;
        var result = GameEngine.Execute(ruleset, before, player.PlayerId, command);
        if (result.IsSuccess)
        {
            State = result.Update.State;
            Record(ruleset, before, result.Update);
            var finished = State.Status == GameStatus.Finished;
            var (phase, currentPlayerId) = (Phase, CurrentPlayerId);
            Phase = finished ? GamePhase.Finished : GamePhase.InProgress;
            CurrentPlayerId = finished ? null : State.CurrentPlayer.Value;
            var markedOut = _players.Aggregate(false, (changed, seat) => seat.MarkOut(State) || changed);
            if (Phase != phase || CurrentPlayerId != currentPlayerId || markedOut)
            {
                ActivityAt = now;
            }

            Revision++;
            AddDomainEvent(new GameChangedEvent(Id));
        }

        return result;
    }

    // Gives every seat the events it may see of an accepted command, as one log entry (none when it saw nothing).
    private void Record(Ruleset ruleset, GameState before, GameUpdate update)
    {
        foreach (var seat in _players)
        {
            var events = VisibilityCalculator.EventsFor(ruleset, before, update, seat.PlayerId);
            if (!events.IsEmpty)
            {
                _logEntries.Add(GameLogEntry.Create(Id, seat.Id, seat.NextLogSequence(), before.Turn, events));
            }
        }
    }

    // A change every player sees: bumps the revision and the activity time, and notifies watchers.
    private void MarkChanged(DateTimeOffset now)
    {
        ActivityAt = now;
        Revision++;
        AddDomainEvent(new GameChangedEvent(Id));
    }

    // The seed decides spawn positions and any later randomness, so it comes from a secure RNG and never leaves the server.
    private static ulong NewSeed() => BinaryPrimitives.ReadUInt64LittleEndian(RandomNumberGenerator.GetBytes(sizeof(ulong)));
}
