using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Games.Models;
using Govern.Application.Games.Services;
using Govern.Application.Users.Models;
using Govern.Domain.Entities;
using Govern.Domain.Enums;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Games.Contracts;

/// <summary>
/// Starts a full lobby (creator only): creates the board with a secret random seed. The creator moves first.
/// </summary>
/// <param name="GameId">Game id.</param>
public sealed record StartGameRequest(Guid GameId) : IRequest<StartGameRequest, GetGameResponse>;

/// <summary>
/// Validates <see cref="StartGameRequest"/>.
/// </summary>
public sealed class StartGameValidator : AbstractValidator<StartGameRequest>
{
    /// <summary>Creates the validation rules.</summary>
    public StartGameValidator() => RuleFor(request => request.GameId).NotEmpty();
}

/// <summary>
/// Handles <see cref="StartGameRequest"/>. A start that loses a race on the game's revision (another start, the joiner
/// leaving, a cancel) reads the game again once and returns the outcome the winner left, e.g.
/// <see cref="GameErrors.AlreadyStarted"/> or <see cref="GameErrors.NotEnoughPlayers"/>.
/// </summary>
internal sealed class StartGameHandler(
    IApplicationDbContextFactory dbContextFactory,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    DefaultRulesetService rulesets,
    MapPoolService maps) : IRequestHandler<StartGameRequest, GetGameResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<GetGameResponse>> HandleAsync(StartGameRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var game = await FindAsync(db.Games, request.GameId, userId, cancellationToken);

        if (game?.FindPlayer(userId) is not { } caller)
        {
            return GameErrors.NotFound;
        }

        if (Check(game, userId) is { } error)
        {
            return error;
        }

        var map = maps.Resolve(game.Map);
        if (map is null && !game.Map.IsGenerated)
        {
            return GameErrors.MapNotInPool;
        }

        game.Start(userId, rulesets.Ruleset, timeProvider.GetUtcNow(), map);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReevaluateAsync(request.GameId, userId, cancellationToken);
        }

        // A game that has just started has no log yet.
        return GetGameResponse.From(game, caller, rulesets.Ruleset, []);
    }

    // Membership is checked in SQL, so an outsider never makes the server load a game's state.
    private static Task<Game?> FindAsync(IQueryable<Game> games, Guid gameId, Guid userId, CancellationToken cancellationToken) =>
        games.Include(candidate => candidate.Players)
            .Where(candidate => candidate.Id == gameId && candidate.Players.Any(player => player.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<Error> ReevaluateAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var game = await FindAsync(db.Games.AsNoTracking(), gameId, userId, cancellationToken);

        // A game that looks startable again (the joiner left and someone else joined) is not retried: the caller reloads.
        return game is null ? GameErrors.NotFound : Check(game, userId) ?? GameErrors.StaleVersion;
    }

    // Why the seated caller may not start the game, or null when they may.
    private static Error? Check(Game game, Guid userId) => game switch
    {
        _ when game.CreatorId != userId => GameErrors.NotCreator,
        { Phase: not GamePhase.Lobby } => GameErrors.AlreadyStarted,
        { IsFull: false } => GameErrors.NotEnoughPlayers,
        _ => null,
    };
}
