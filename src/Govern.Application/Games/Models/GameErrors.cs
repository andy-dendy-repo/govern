using Govern.Utils.Results;

namespace Govern.Application.Games.Models;

/// <summary>
/// Errors returned by the Games use cases. A game the caller does not play in is always <see cref="NotFound"/>, never
/// Forbidden, so the Api does not reveal which games exist.
/// </summary>
public static class GameErrors
{
    /// <summary>How many lobbies (games still in the lobby phase) one user may have created at a time.</summary>
    public const int MaxOpenLobbiesPerUser = 5;

    /// <summary>The caller already created <see cref="MaxOpenLobbiesPerUser"/> lobbies that have not started.</summary>
    public static readonly Error TooManyOpenLobbies = Error.Conflict(
        "game.too_many_open_lobbies", $"You already have {MaxOpenLobbiesPerUser} open lobbies. Start one before creating another.");

    /// <summary>No game uses the invite code.</summary>
    public static readonly Error InviteNotFound = Error.NotFound("game.invite_not_found", "No game uses this invite code.");

    /// <summary>The game does not exist or the caller does not play in it (deliberately indistinguishable).</summary>
    public static readonly Error NotFound = Error.NotFound("game.not_found", "The game was not found.");

    /// <summary>A player other than the creator tried to start the game.</summary>
    public static readonly Error NotCreator = Error.Forbidden("game.not_creator", "Only the creator can start the game.");

    /// <summary>The game has left the lobby, so it can no longer be joined or started.</summary>
    public static readonly Error AlreadyStarted = Error.Conflict("game.already_started", "The game has already started.");

    /// <summary>Every seat is taken.</summary>
    public static readonly Error Full = Error.Conflict("game.full", "The game is full.");

    /// <summary>The creator tried to start before a second player joined.</summary>
    public static readonly Error NotEnoughPlayers = Error.Conflict("game.not_enough_players", "The game needs two players to start.");
}
