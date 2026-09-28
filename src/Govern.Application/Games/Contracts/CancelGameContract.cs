using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Games.Models;
using Govern.Application.Users.Models;
using Govern.Domain.Entities;
using Govern.Domain.Enums;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Games.Contracts;

/// <summary>
/// Cancels a lobby (creator only): the game is deleted, its invite code stops working and both players' seats are freed.
/// A running game is ended with the <c>resign</c> command instead.
/// </summary>
/// <param name="GameId">Game id.</param>
public sealed record CancelGameRequest(Guid GameId) : IRequest<CancelGameRequest, CancelGameResponse>;

/// <summary>
/// The lobby was deleted.
/// </summary>
public sealed record CancelGameResponse : IResponse;

/// <summary>
/// Validates <see cref="CancelGameRequest"/>.
/// </summary>
public sealed class CancelGameValidator : AbstractValidator<CancelGameRequest>
{
    /// <summary>Creates the validation rules.</summary>
    public CancelGameValidator() => RuleFor(request => request.GameId).NotEmpty();
}

/// <summary>
/// Handles <see cref="CancelGameRequest"/>. The delete is guarded by the game's revision, so a cancel that loses a race
/// (the lobby was started first) reads the game again once and returns the outcome the winner left.
/// </summary>
internal sealed class CancelGameHandler(IApplicationDbContextFactory dbContextFactory, ICurrentUserService currentUser)
    : IRequestHandler<CancelGameRequest, CancelGameResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<CancelGameResponse>> HandleAsync(CancelGameRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var game = await FindAsync(db.Games, request.GameId, userId, cancellationToken);
        if (game is null)
        {
            return GameErrors.NotFound;
        }

        if (Check(game, userId) is { } error)
        {
            return error;
        }

        game.Cancel(userId);
        db.Games.Remove(game);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReevaluateAsync(request.GameId, userId, cancellationToken);
        }

        return new CancelGameResponse();
    }

    // Why the caller may not cancel, or null when they may.
    private static Error? Check(Game game, Guid userId) => game switch
    {
        _ when game.CreatorId != userId => GameErrors.NotCreator,
        { Phase: not GamePhase.Lobby } => GameErrors.AlreadyStarted,
        _ => null,
    };

    // Membership is checked in SQL, so an outsider learns nothing about the game.
    private static Task<Game?> FindAsync(IQueryable<Game> games, Guid gameId, Guid userId, CancellationToken cancellationToken) =>
        games.Include(candidate => candidate.Players)
            .Where(candidate => candidate.Id == gameId && candidate.Players.Any(player => player.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<Error> ReevaluateAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var game = await FindAsync(db.Games.AsNoTracking(), gameId, userId, cancellationToken);

        // Still a lobby: it changed in between (a join or leave), so the caller reloads and may cancel again.
        return game is null ? GameErrors.NotFound : Check(game, userId) ?? GameErrors.StaleVersion;
    }
}
