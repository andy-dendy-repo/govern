using System.Runtime.CompilerServices;
using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Games.Models;
using Govern.Application.Games.Services;
using Govern.Application.Users.Models;
using Govern.Domain.Enums;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Games.Contracts;

/// <summary>
/// Watches a game the caller plays in: the current snapshot, then a new one whenever what the caller sees changes.
/// </summary>
/// <param name="GameId">Game id.</param>
public sealed record WatchGameRequest(Guid GameId) : IRequest<WatchGameRequest, WatchGameResponse>;

/// <summary>
/// The caller's snapshots of a game, read lazily while <see cref="Snapshots"/> is enumerated.
/// </summary>
/// <param name="Snapshots">
/// The game as <c>GET /api/games/{id}</c> returns it: first the current state, then one snapshot each time the caller's phase,
/// seats or view change. A change hidden from the caller (fog of war) produces nothing. The sequence ends after the finished
/// snapshot, or when the caller can no longer read the game. It waits for changes until its enumeration is cancelled.
/// </param>
public sealed record WatchGameResponse(IAsyncEnumerable<GetGameResponse> Snapshots) : IResponse;

/// <summary>
/// Validates <see cref="WatchGameRequest"/>.
/// </summary>
public sealed class WatchGameValidator : AbstractValidator<WatchGameRequest>
{
    /// <summary>Creates the validation rules.</summary>
    public WatchGameValidator() => RuleFor(request => request.GameId).NotEmpty();
}

/// <summary>
/// Handles <see cref="WatchGameRequest"/>. Membership is checked before anything is streamed, so an outsider gets
/// <see cref="GameErrors.NotFound"/>; each snapshot is read again from the database for the caller.
/// </summary>
internal sealed class WatchGameHandler(
    IApplicationDbContextFactory dbContextFactory,
    ICurrentUserService currentUser,
    IGameChangeService gameChanges,
    DefaultRulesetService rulesets) : IRequestHandler<WatchGameRequest, WatchGameResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<WatchGameResponse>> HandleAsync(WatchGameRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var isPlayer = await db.Games.AnyAsync(
            candidate => candidate.Id == request.GameId && candidate.Players.Any(player => player.UserId == userId), cancellationToken);

        return isPlayer ? new WatchGameResponse(SnapshotsAsync(request.GameId, userId, cancellationToken)) : GameErrors.NotFound;
    }

    private async IAsyncEnumerable<GetGameResponse> SnapshotsAsync(
        Guid gameId,
        Guid userId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Subscribe before the first read, so a change saved in between still wakes the loop.
        using var subscription = gameChanges.Subscribe(gameId);
        (GamePhase Phase, int Players, long? View)? sent = null;

        while (true)
        {
            if (await LoadAsync(gameId, userId, cancellationToken) is not { } snapshot)
            {
                yield break;
            }

            // Only what the caller sees counts: a change hidden by the fog of war leaves all three unchanged.
            var seen = (snapshot.Phase, snapshot.Players.Count, snapshot.View?.Version);
            if (seen != sent)
            {
                sent = seen;
                yield return snapshot;
            }

            if (snapshot.Phase == GamePhase.Finished)
            {
                yield break;
            }

            await subscription.WaitAsync(cancellationToken);
        }
    }

    private async Task<GetGameResponse?> LoadAsync(Guid gameId, Guid userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var game = await db.Games
            .AsNoTracking()
            .Include(candidate => candidate.Players)
            .Where(candidate => candidate.Id == gameId && candidate.Players.Any(player => player.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

        if (game?.FindPlayer(userId) is not { } caller)
        {
            return null;
        }

        var log = await GameLogService.RecentAsync(db, game, caller, cancellationToken);
        return GetGameResponse.From(game, caller, rulesets.Ruleset, log);
    }
}
