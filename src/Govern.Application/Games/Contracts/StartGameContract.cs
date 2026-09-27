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
/// Handles <see cref="StartGameRequest"/>. Two concurrent starts race on the game's revision; the loser gets
/// <see cref="GameErrors.AlreadyStarted"/>.
/// </summary>
internal sealed class StartGameHandler(IApplicationDbContextFactory dbContextFactory, ICurrentUserService currentUser)
    : IRequestHandler<StartGameRequest, GetGameResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<GetGameResponse>> HandleAsync(StartGameRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Membership is checked in SQL, so an outsider never makes the server load a game's state.
        var game = await db.Games
            .Include(candidate => candidate.Players)
            .Where(candidate => candidate.Id == request.GameId && candidate.Players.Any(player => player.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

        if (game?.FindPlayer(userId) is not { } caller)
        {
            return GameErrors.NotFound;
        }

        if (Check(game, userId) is { } error)
        {
            return error;
        }

        game.Start(userId);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return GameErrors.AlreadyStarted;
        }

        return GetGameResponse.From(game, caller);
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
