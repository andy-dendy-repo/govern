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
/// Creates a lobby for the caller, who takes seat 0. Share the returned invite code with the other players.
/// </summary>
/// <param name="Map">
/// The map to play on: <c>random</c> (any pool map for this many seats, drawn at the start, or a generated map when the pool
/// has none), <c>generated</c>, or the key of a pool map made for this many seats (<see cref="MapPoolService"/>);
/// <see langword="null"/> for <c>random</c>.
/// </param>
/// <param name="Seats">How many players the game is for, within the ruleset's range (2 to 4); 2 when left out.</param>
public sealed record CreateGameRequest(string? Map = null, int Seats = Game.MinSeats) : IRequest<CreateGameRequest, CreateGameResponse>;

/// <summary>
/// The new lobby.
/// </summary>
/// <param name="Id">Game id.</param>
/// <param name="InviteCode">The secret code other users join with; share it only with the intended players.</param>
public sealed record CreateGameResponse(Guid Id, string InviteCode) : IResponse;

/// <summary>
/// Validates <see cref="CreateGameRequest"/>: the seats are within the ruleset's range and the map is a choice the pool offers
/// for them.
/// </summary>
public sealed class CreateGameValidator : AbstractValidator<CreateGameRequest>
{
    /// <summary>Creates the validation rules.</summary>
    /// <param name="maps">The map pool.</param>
    /// <param name="rulesets">The ruleset games are played with (its player range).</param>
    public CreateGameValidator(MapPoolService maps, DefaultRulesetService rulesets)
    {
        ArgumentNullException.ThrowIfNull(rulesets);
        var setup = rulesets.Ruleset.Setup;
        RuleFor(request => request.Seats)
            .InclusiveBetween(setup.MinPlayers, setup.MaxPlayers);
        RuleFor(request => request.Map)
            .Must((request, map) => map is null || maps.IsChoice(map, request.Seats))
            .When(request => setup.Allows(request.Seats))
            .WithMessage("'Map' must be 'random', 'generated' or the key of a map in the pool made for this many seats.");
    }
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

        var game = Game.Create(userId, now, request.Map is { } map ? MapChoice.Create(map) : MapChoice.Random, request.Seats);
        db.Games.Add(game);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateGameResponse(game.Id, game.InviteCode.Value);
    }
}
