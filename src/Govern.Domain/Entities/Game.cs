using System.Buffers.Binary;
using System.Security.Cryptography;
using Govern.Core.State;
using Govern.Domain.EntityBase;
using Govern.Domain.Enums;
using Govern.Domain.ValueObjects;

namespace Govern.Domain.Entities;

/// <summary>
/// A two-player game: a lobby joined through an invite code, then a running game whose whole <see cref="GameState"/> is
/// stored as one snapshot. The aggregate root for <see cref="GamePlayer"/>. Every change increments <see cref="Revision"/>,
/// which the persistence layer uses as an optimistic concurrency token.
/// </summary>
/// <remarks>
/// The methods throw on calls the rules forbid (joining a full game, starting as a non-creator): handlers check those
/// conditions first and return errors, so a throw here is a programming error.
/// </remarks>
public sealed class Game : BaseAuditableEntity
{
    /// <summary>Number of seats.</summary>
    public const int MaxPlayers = GameSetup.PlayerCount;

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
        var game = new Game { CreatorId = creatorId, InviteCode = InviteCode.New(), Phase = GamePhase.Lobby };
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
        Revision++;
        return player;
    }

    /// <summary>
    /// Starts the game: creates the engine state with the players in seat order and a secret random seed.
    /// </summary>
    /// <param name="userId">The user starting the game.</param>
    /// <exception cref="InvalidOperationException">
    /// The user is not the creator, the game is not in the lobby, or it is not full.
    /// </exception>
    public void Start(Guid userId)
    {
        if (userId != CreatorId || Phase != GamePhase.Lobby || !IsFull)
        {
            throw new InvalidOperationException("Only the creator can start a full lobby.");
        }

        var players = _players.OrderBy(static player => player.Seat).Select(static player => player.PlayerId);
        State = GameFactory.Create(new GameSetup([.. players], NewSeed()));
        Phase = GamePhase.InProgress;
        Revision++;
    }

    // The seed decides spawn positions and any later randomness, so it comes from a secure RNG and never leaves the server.
    private static ulong NewSeed() => BinaryPrimitives.ReadUInt64LittleEndian(RandomNumberGenerator.GetBytes(sizeof(ulong)));
}
