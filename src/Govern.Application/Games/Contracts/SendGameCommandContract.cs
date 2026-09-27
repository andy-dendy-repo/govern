using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Games.Models;
using Govern.Application.Users.Models;
using Govern.Core.Engine;
using Govern.Domain.Entities;
using Govern.Domain.Enums;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Games.Contracts;

/// <summary>
/// Sends one of the caller's commands (move, attack, found city, set production, end turn, resign) to a running game.
/// </summary>
/// <param name="GameId">Game id.</param>
/// <param name="ExpectedVersion">
/// The <c>view.version</c> the command is based on. When the caller's view has changed since, the command is rejected with
/// <see cref="GameErrors.StaleVersion"/> instead of being applied to a board the player has not seen. The version is a
/// fingerprint of the caller's own view, so changes hidden from them never make their command stale.
/// </param>
/// <param name="Command">The command.</param>
public sealed record SendGameCommandRequest(Guid GameId, long ExpectedVersion, GameCommand Command)
    : IRequest<SendGameCommandRequest, GetGameResponse>;

/// <summary>
/// Validates <see cref="SendGameCommandRequest"/>.
/// </summary>
public sealed class SendGameCommandValidator : AbstractValidator<SendGameCommandRequest>
{
    /// <summary>Creates the validation rules.</summary>
    public SendGameCommandValidator()
    {
        RuleFor(request => request.GameId).NotEmpty();
        RuleFor(request => request.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(request => request.Command).NotNull();
    }
}

/// <summary>
/// Handles <see cref="SendGameCommandRequest"/>. Two concurrent commands race on the game's revision; the loser gets
/// <see cref="GameErrors.StaleVersion"/>. A rule violation becomes a conflict with the violation's code.
/// </summary>
internal sealed class SendGameCommandHandler(IApplicationDbContextFactory dbContextFactory, ICurrentUserService currentUser)
    : IRequestHandler<SendGameCommandRequest, GetGameResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<GetGameResponse>> HandleAsync(SendGameCommandRequest request, CancellationToken cancellationToken)
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

        if (Check(game, caller, request.ExpectedVersion) is { } error)
        {
            return error;
        }

        var result = game.Execute(userId, request.Command);
        if (!result.IsSuccess)
        {
            return GameErrors.FromViolation(result.Violation);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return GameErrors.StaleVersion;
        }

        return GetGameResponse.From(game, caller);
    }

    // Why the command cannot reach the engine, or null when it can (a finished game is left to the engine: game.finished).
    private static Error? Check(Game game, GamePlayer caller, long expectedVersion) => game switch
    {
        { Phase: GamePhase.Lobby } or { State: null } => GameErrors.NotStarted,
        { State: var state } when GameEngine.ViewFor(state, caller.PlayerId).Version != expectedVersion => GameErrors.StaleVersion,
        _ => null,
    };
}
