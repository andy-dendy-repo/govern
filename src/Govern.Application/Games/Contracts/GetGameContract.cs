using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Games.Models;
using Govern.Application.Games.Services;
using Govern.Application.Users.Models;
using Govern.Core.Engine;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Visibility;
using Govern.Domain.Entities;
using Govern.Domain.Enums;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Games.Contracts;

/// <summary>
/// Returns a game the caller plays in: the lobby details and, once started, the caller's fog-of-war view.
/// </summary>
/// <param name="GameId">Game id.</param>
public sealed record GetGameRequest(Guid GameId) : IRequest<GetGameRequest, GetGameResponse>;

/// <summary>
/// A game as one player sees it. Contains no emails and no user ids.
/// </summary>
/// <param name="Id">Game id.</param>
/// <param name="Phase">Lobby, in progress or finished.</param>
/// <param name="IsCreator">Whether the caller created the game (and so may start it).</param>
/// <param name="InviteCode">
/// The invite code, only for the creator and only while in the lobby; <see langword="null"/> otherwise (only the creator shares it).
/// </param>
/// <param name="Seats">How many players the game is for; the lobby starts once <paramref name="Players"/> has this many.</param>
/// <param name="Players">Seats in order.</param>
/// <param name="View">The caller's view of the board once started; <see langword="null"/> in the lobby.</param>
/// <param name="CreatedAt">When the game was created.</param>
/// <param name="Map">
/// The map: <c>generated</c>, a pool map's key (its name is in the map pool, which the client embeds too) or, in the lobby only,
/// <c>random</c>.
/// </param>
/// <param name="Log">
/// The caller's recent game log (<see cref="GameLogService"/>), oldest first: only the events they may see. Empty in the lobby.
/// </param>
/// <param name="TurnMode">How the players take their turns (sequential or simultaneous), chosen with the lobby.</param>
public sealed record GetGameResponse(
    Guid Id,
    GamePhase Phase,
    bool IsCreator,
    string? InviteCode,
    int Seats,
    IReadOnlyList<GamePlayerSummary> Players,
    PlayerView? View,
    DateTimeOffset CreatedAt,
    string Map,
    IReadOnlyList<GameLogItem> Log,
    TurnMode TurnMode = TurnMode.Sequential) : IResponse
{
    /// <summary>Maps a loaded game (with its players) for one of its players.</summary>
    /// <param name="game">The game.</param>
    /// <param name="caller">The caller's seat in <paramref name="game"/>.</param>
    /// <param name="ruleset">The ruleset the game is played with.</param>
    /// <param name="log">The caller's recent log (<see cref="GameLogService.RecentAsync"/>).</param>
    /// <returns>The response; <see cref="View"/> is built with <see cref="GameEngine.ViewFor"/>.</returns>
    public static GetGameResponse From(Game game, GamePlayer caller, Ruleset ruleset, IReadOnlyList<GameLogItem> log)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(log);

        var players = game.Players
            .OrderBy(static player => player.Seat)
            .Select(player => GamePlayerSummary.From(player, caller.UserId, game.CreatorId))
            .ToList();

        var isCreator = game.CreatorId == caller.UserId;
        return new GetGameResponse(
            game.Id,
            game.Phase,
            isCreator,
            isCreator && game.Phase == GamePhase.Lobby ? game.InviteCode.Value : null,
            game.Seats,
            players,
            game.State is { } state ? GameEngine.ViewFor(ruleset, state, caller.PlayerId) : null,
            game.CreatedAt,
            game.Map.Value,
            log,
            game.TurnMode);
    }
}

/// <summary>
/// Validates <see cref="GetGameRequest"/>.
/// </summary>
public sealed class GetGameValidator : AbstractValidator<GetGameRequest>
{
    /// <summary>Creates the validation rules.</summary>
    public GetGameValidator() => RuleFor(request => request.GameId).NotEmpty();
}

/// <summary>
/// Handles <see cref="GetGameRequest"/>. The whole aggregate is read (no tracking), since the view needs the full state.
/// </summary>
internal sealed class GetGameHandler(
    IApplicationDbContextFactory dbContextFactory,
    ICurrentUserService currentUser,
    DefaultRulesetService rulesets)
    : IRequestHandler<GetGameRequest, GetGameResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<GetGameResponse>> HandleAsync(GetGameRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var game = await db.Games
            .AsNoTracking()
            .Include(candidate => candidate.Players)
            .Where(candidate => candidate.Id == request.GameId && candidate.Players.Any(player => player.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

        if (game?.FindPlayer(userId) is not { } caller)
        {
            return GameErrors.NotFound;
        }

        var log = await GameLogService.RecentAsync(db, game, caller, cancellationToken);
        return GetGameResponse.From(game, caller, rulesets.Ruleset, log);
    }
}
