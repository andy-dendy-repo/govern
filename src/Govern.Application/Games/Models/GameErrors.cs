using Govern.Core.Engine;
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

    /// <summary>How many games that have not finished (lobbies and running games) one user may play in at a time.</summary>
    public const int MaxUnfinishedGamesPerUser = 20;

    /// <summary>
    /// How many seats one user may take (by creating or joining a game) within <see cref="QuotaWindow"/>, finished games
    /// included. A seat given up by leaving or cancelling a lobby no longer counts.
    /// </summary>
    public const int MaxGamesJoinedPerWindow = 30;

    /// <summary>The rolling window of <see cref="MaxGamesJoinedPerWindow"/>.</summary>
    public static readonly TimeSpan QuotaWindow = TimeSpan.FromHours(24);

    /// <summary>The caller already created <see cref="MaxOpenLobbiesPerUser"/> lobbies that have not started.</summary>
    public static readonly Error TooManyOpenLobbies = Error.Conflict(
        "game.too_many_open_lobbies",
        $"You already have {MaxOpenLobbiesPerUser} open lobbies. Start or cancel one before creating another.");

    /// <summary>The caller already plays in <see cref="MaxUnfinishedGamesPerUser"/> games that have not finished.</summary>
    public static readonly Error TooManyUnfinishedGames = Error.Conflict(
        "game.too_many_unfinished_games",
        $"You already play in {MaxUnfinishedGamesPerUser} unfinished games. Finish or resign a running game, or leave or cancel a lobby.");

    /// <summary>The caller already took <see cref="MaxGamesJoinedPerWindow"/> seats within <see cref="QuotaWindow"/>.</summary>
    public static readonly Error DailyQuotaReached = Error.TooManyRequests(
        "game.daily_quota_reached",
        $"You have created or joined {MaxGamesJoinedPerWindow} games in the last 24 hours. Try again later.");

    /// <summary>No game uses the invite code.</summary>
    public static readonly Error InviteNotFound = Error.NotFound("game.invite_not_found", "No game uses this invite code.");

    /// <summary>The game does not exist or the caller does not play in it (deliberately indistinguishable).</summary>
    public static readonly Error NotFound = Error.NotFound("game.not_found", "The game was not found.");

    /// <summary>A player other than the creator tried to start or cancel the game.</summary>
    public static readonly Error NotCreator = Error.Forbidden("game.not_creator", "Only the creator can start or cancel the game.");

    /// <summary>The creator tried to leave their own lobby; they cancel it instead.</summary>
    public static readonly Error CreatorCannotLeave = Error.Conflict(
        "game.creator_cannot_leave", "The creator cannot leave the lobby. Cancel it instead.");

    /// <summary>The game has left the lobby, so it can no longer be joined, started, left or cancelled.</summary>
    public static readonly Error AlreadyStarted = Error.Conflict("game.already_started", "The game has already started.");

    /// <summary>Every seat is taken.</summary>
    public static readonly Error Full = Error.Conflict("game.full", "The game is full.");

    /// <summary>The creator tried to start before a second player joined.</summary>
    public static readonly Error NotEnoughPlayers = Error.Conflict("game.not_enough_players", "Every seat must be taken before the game starts.");

    /// <summary>The lobby's map was removed from the pool after the lobby was created; cancel it and create a new one.</summary>
    public static readonly Error MapNotInPool = Error.Conflict(
        "game.map_not_in_pool", "The map this lobby chose is no longer available. Create a new game.");

    /// <summary>A command was sent to a game that is still in the lobby.</summary>
    public static readonly Error NotStarted = Error.Conflict("game.not_started", "The game has not started yet.");

    /// <summary>
    /// The command was based on an older version of the game (another command was applied first); read the game again.
    /// </summary>
    public static readonly Error StaleVersion = Error.Conflict(
        "game.stale_version", "The game has changed since you last read it. Reload it and try again.");

    /// <summary>
    /// Converts an engine rule violation to a conflict with the same code: the command conflicts with the current state.
    /// </summary>
    /// <param name="violation">The violation returned by the engine.</param>
    /// <returns>A <see cref="ErrorType.Conflict"/> error.</returns>
    public static Error FromViolation(RuleViolation violation)
    {
        ArgumentNullException.ThrowIfNull(violation);
        return Error.Conflict(violation.Code, violation.Message);
    }
}
