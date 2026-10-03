using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Games.Models;
using Govern.Application.Games.Services;
using Govern.Application.Users.Models;
using Govern.Domain.Entities;
using Govern.Domain.Enums;
using Govern.Domain.ValueObjects;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Games.Contracts;

/// <summary>
/// Creates a lobby for the caller, who takes seat 0. Share the returned invite code with the second player.
/// </summary>
/// <param name="Map">
/// The map to play on: <c>random</c> (any pool map, drawn at the start), <c>generated</c>, or a pool map's key
/// (<see cref="MapPoolService"/>); <see langword="null"/> for <c>random</c>.
/// </param>
public sealed record CreateGameRequest(string? Map = null) : IRequest<CreateGameRequest, CreateGameResponse>;

/// <summary>
/// The new lobby.
/// </summary>
/// <param name="Id">Game id.</param>
/// <param name="InviteCode">The secret code another user joins with; share it only with the intended player.</param>
public sealed record CreateGameResponse(Guid Id, string InviteCode) : IResponse;

/// <summary>
/// Validates <see cref="CreateGameRequest"/>: the map is a choice the pool offers.
/// </summary>
public sealed class CreateGameValidator : AbstractValidator<CreateGameRequest>
{
    /// <summary>Creates the validation rules.</summary>
    /// <param name="maps">The map pool.</param>
    public CreateGameValidator(MapPoolService maps) =>
        RuleFor(request => request.Map)
            .Must(map => map is null || maps.IsChoice(map))
            .WithMessage("'Map' must be 'random', 'generated' or the key of a map in the pool.");
}

/// <summary>
/// Handles <see cref="CreateGameRequest"/>; a user may hold at most <see cref="GameErrors.MaxOpenLobbiesPerUser"/> unstarted
/// lobbies, and the seat limits of <see cref="GameSeatLimitService"/> apply. The checks are not locked, so simultaneous
/// creates can overshoot them: soft caps, bounded by the per-user rate limit.
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

        var now = timeProvider.GetUtcNow();
        if (await GameSeatLimitService.CheckAsync(db, userId, now, cancellationToken) is { } limit)
        {
            return limit;
        }

        var game = Game.Create(userId, now, request.Map is { } map ? MapChoice.Create(map) : MapChoice.Random);
        db.Games.Add(game);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateGameResponse(game.Id, game.InviteCode.Value);
    }
}
