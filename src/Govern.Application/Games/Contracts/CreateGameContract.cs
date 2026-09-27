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
/// Creates a lobby for the caller, who takes seat 0. Share the returned invite code with the second player.
/// </summary>
public sealed record CreateGameRequest : IRequest<CreateGameRequest, CreateGameResponse>;

/// <summary>
/// The new lobby.
/// </summary>
/// <param name="Id">Game id.</param>
/// <param name="InviteCode">The secret code another user joins with; share it only with the intended player.</param>
public sealed record CreateGameResponse(Guid Id, string InviteCode) : IResponse;

/// <summary>
/// Handles <see cref="CreateGameRequest"/>; a user may hold at most <see cref="GameErrors.MaxOpenLobbiesPerUser"/> unstarted
/// lobbies and play in at most <see cref="GameErrors.MaxUnfinishedGamesPerUser"/> unfinished games. The checks are not
/// locked, so simultaneous creates can overshoot them: soft caps, bounded by the per-user rate limit.
/// </summary>
internal sealed class CreateGameHandler(
    IApplicationDbContextFactory dbContextFactory,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IRequestHandler<CreateGameRequest, CreateGameResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<CreateGameResponse>> HandleAsync(CreateGameRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var openLobbies = await db.Games.CountAsync(game => game.CreatorId == userId && game.Phase == GamePhase.Lobby, cancellationToken);
        if (openLobbies >= GameErrors.MaxOpenLobbiesPerUser)
        {
            return GameErrors.TooManyOpenLobbies;
        }

        var unfinished = await db.Games.CountAsync(
            game => game.Phase != GamePhase.Finished && game.Players.Any(player => player.UserId == userId), cancellationToken);
        if (unfinished >= GameErrors.MaxUnfinishedGamesPerUser)
        {
            return GameErrors.TooManyUnfinishedGames;
        }

        var game = Game.Create(userId, timeProvider.GetUtcNow());
        db.Games.Add(game);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateGameResponse(game.Id, game.InviteCode.Value);
    }
}
