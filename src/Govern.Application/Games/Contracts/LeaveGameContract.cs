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
/// Gives up the caller's seat in a lobby they joined, so it no longer counts toward their limits. The creator cancels the
/// lobby instead; a running game is ended with the <c>resign</c> command.
/// </summary>
/// <param name="GameId">Game id.</param>
public sealed record LeaveGameRequest(Guid GameId) : IRequest<LeaveGameRequest, LeaveGameResponse>;

/// <summary>
/// The seat was given up; the caller can no longer read the game, but may join it again with the invite code.
/// </summary>
public sealed record LeaveGameResponse : IResponse;

/// <summary>
/// Validates <see cref="LeaveGameRequest"/>.
/// </summary>
public sealed class LeaveGameValidator : AbstractValidator<LeaveGameRequest>
{
    /// <summary>Creates the validation rules.</summary>
    public LeaveGameValidator() => RuleFor(request => request.GameId).NotEmpty();
}

/// <summary>
/// Handles <see cref="LeaveGameRequest"/>. A leave that loses a race on the game's revision (the creator started or
/// cancelled it first) reads the game again once and returns the outcome the winner left.
/// </summary>
internal sealed class LeaveGameHandler(
    IApplicationDbContextFactory dbContextFactory,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IRequestHandler<LeaveGameRequest, LeaveGameResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<LeaveGameResponse>> HandleAsync(LeaveGameRequest request, CancellationToken cancellationToken)
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

        game.Leave(userId, timeProvider.GetUtcNow());
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReevaluateAsync(request.GameId, userId, cancellationToken);
        }

        return new LeaveGameResponse();
    }

    // Why the caller may not leave, or null when they may.
    private static Error? Check(Game game, Guid userId) => game switch
    {
        _ when game.CreatorId == userId => GameErrors.CreatorCannotLeave,
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

        // Still a lobby the caller sits in: something else changed it (e.g. a second request), so the caller reloads.
        return game is null ? GameErrors.NotFound : Check(game, userId) ?? GameErrors.StaleVersion;
    }
}
