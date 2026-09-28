using System.Buffers.Binary;
using System.Security.Cryptography;
using Govern.Core.Engine;
using Govern.Core.State;
using Govern.Domain.EntityBase;
using Govern.Domain.Enums;
using Govern.Domain.Events;
using Govern.Domain.ValueObjects;

namespace Govern.Domain.Entities;

/// <summary>
/// A two-player game: a lobby joined through an invite code, then a running game whose whole <see cref="GameState"/> is
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
    /// <summary>Number of seats.</summary>
    public const int MaxPlayers = GameSetup.PlayerCount;

    /// <summary>
    /// How long a lobby may go without activity (<see cref="ActivityAt"/>) before it expires and is deleted.
    /// </summary>
    public static readonly TimeSpan LobbyLifetime = TimeSpan.FromDays(7);

    private readonly List<GamePlayer> _players = [];

    private Game()
    {
    }

    /// <summary>
    /// The user who created the game; only they may start it.
    /// </summary>
    public Guid CreatorId { get; private set; }

    /// <summary>
    /// The secret that lets another user join while the game is in the <see cref="GamePhase.Lobby"/>; unique across games.
    /// </summary>
    public InviteCode InviteCode { get; private set; } = null!;

    /// <summary>
    /// Where the game is in its lifecycle.
    /// </summary>
    public GamePhase Phase { get; private set; }

    /// <summary>
    /// Incremented by every change, so two concurrent changes to the same game cannot both be saved.
    /// </summary>
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
    /// When the game last changed in a way every player sees: created, a seat taken or left, started, the turn passed or the
    /// game finished. Unlike <see cref="BaseAuditableEntity.UpdatedAt"/>, a command whose effect may be hidden from the
    /// opponent (a move, a production change) leaves it alone, so it reveals nothing about hidden activity.
    /// </summary>
    public DateTimeOffset ActivityAt { get; private set; }

    /// <summary>
    /// The seated players (the caller must load them).
    /// </summary>
    public IReadOnlyCollection<GamePlayer> Players => _players.AsReadOnly();

    /// <summary>
    /// Whether every seat is taken.
    /// </summary>
    public bool IsFull => _players.Count >= MaxPlayers;

    /// <summary>
    /// Creates a lobby with a new invite code; the creator takes seat 0.
    /// </summary>
    /// <param name="creatorId">The creating user.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The new game.</returns>
    /// <exception cref="ArgumentException"><paramref name="creatorId"/> is empty.</exception>
    public static Game Create(Guid creatorId, DateTimeOffset now)
    {
        var game = new Game { CreatorId = creatorId, InviteCode = InviteCode.New(), Phase = GamePhase.Lobby, ActivityAt = now };
        game._players.Add(GamePlayer.Create(game.Id, creatorId, seat: 0, now));
        return game;
    }

    /// <summary>
    /// Returns the seat of <paramref name="userId"/>.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns>The player, or <see langword="null"/> when the user is not in this game.</returns>
    public GamePlayer? FindPlayer(Guid userId) => _players.Find(player => player.UserId == userId);

    /// <summary>
    /// Whether <paramref name="userId"/> has a seat in this game.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <returns><see langword="true"/> for a seated player.</returns>
    public bool IsPlayer(Guid userId) => FindPlayer(userId) is not null;

    /// <summary>
    /// Seats <paramref name="userId"/> in the next free seat. Joining again returns the existing seat and changes nothing.
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

        var player = GamePlayer.Create(Id, userId, _players.Count, now);
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

        // Two seats and the creator can't leave, so only seat 1 is ever freed and Join's next seat (_players.Count) is 1 again.
        // More seats would need Join to take the lowest free seat instead.
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
    /// Starts the game: creates the engine state with the players in seat order and a secret random seed.
    /// </summary>
    /// <param name="userId">The user starting the game.</param>
    /// <param name="now">The current time.</param>
    /// <exception cref="InvalidOperationException">
    /// The user is not the creator, the game is not in the lobby, or it is not full.
    /// </exception>
    public void Start(Guid userId, DateTimeOffset now)
    {
        if (userId != CreatorId || Phase != GamePhase.Lobby || !IsFull)
        {
            throw new InvalidOperationException("Only the creator can start a full lobby.");
        }

        var players = _players.OrderBy(static player => player.Seat).Select(static player => player.PlayerId);
        State = GameFactory.Create(new GameSetup([.. players], NewSeed()));
        Phase = GamePhase.InProgress;
        CurrentPlayerId = State.CurrentPlayer.Value;
        MarkChanged(now);
    }

    /// <summary>
    /// Runs one player's command through the engine. An accepted command replaces <see cref="State"/>, moves
    /// <see cref="CurrentPlayerId"/> and finishes the game when the engine does; a rejected one changes nothing.
    /// <see cref="ActivityAt"/> moves only when the turn passes or the game finishes.
    /// </summary>
    /// <param name="userId">The seated user sending the command.</param>
    /// <param name="command">The command.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The engine's result: the update, or the <see cref="RuleViolation"/> that rejected the command.</returns>
    /// <exception cref="InvalidOperationException">The game has not started, or the user has no seat in it.</exception>
    public CommandResult Execute(Guid userId, GameCommand command, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (State is null || FindPlayer(userId) is not { } player)
        {
            throw new InvalidOperationException("Only a seated player of a started game can send commands.");
        }

        var result = GameEngine.Execute(State, player.PlayerId, command);
        if (result.IsSuccess)
        {
            State = result.Update.State;
            var finished = State.Status == GameStatus.Finished;
            var (phase, currentPlayerId) = (Phase, CurrentPlayerId);
            Phase = finished ? GamePhase.Finished : GamePhase.InProgress;
            CurrentPlayerId = finished ? null : State.CurrentPlayer.Value;
            if (Phase != phase || CurrentPlayerId != currentPlayerId)
            {
                ActivityAt = now;
            }

            Revision++;
            AddDomainEvent(new GameChangedEvent(Id));
        }

        return result;
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
