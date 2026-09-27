using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Games.Models;
using Govern.Application.Users.Models;
using Govern.Domain.Entities;
using Govern.Domain.Enums;
using Govern.Domain.ValueObjects;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Games.Contracts;

/// <summary>
/// Takes the free seat of the lobby that uses <paramref name="InviteCode"/>. Joining a game you already play in succeeds and
/// changes nothing.
/// </summary>
/// <param name="InviteCode">The code from the invite link (sent in the body so it never appears in URLs or access logs).</param>
public sealed record JoinGameRequest(string InviteCode) : IRequest<JoinGameRequest, JoinGameResponse>;

/// <summary>
/// The joined game.
/// </summary>
/// <param name="GameId">Game id.</param>
public sealed record JoinGameResponse(Guid GameId) : IResponse;

/// <summary>
/// Validates <see cref="JoinGameRequest"/>.
/// </summary>
public sealed class JoinGameValidator : AbstractValidator<JoinGameRequest>
{
    /// <summary>Creates the validation rules.</summary>
    public JoinGameValidator() =>
        RuleFor(request => request.InviteCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(InviteCode.IsValid)
            .WithMessage("'Invite Code' is not a valid invite code.");
}

/// <summary>
/// Handles <see cref="JoinGameRequest"/>. A lost race (another join changed the game first: a revision conflict, or a
/// unique seat/user violation) is resolved by reading the game again once, so the caller gets the outcome the winner left.
/// </summary>
internal sealed class JoinGameHandler(
    IApplicationDbContextFactory dbContextFactory,
    ICurrentUserService currentUser,
    IDbExceptionClassificationService dbExceptions,
    TimeProvider timeProvider) : IRequestHandler<JoinGameRequest, JoinGameResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<JoinGameResponse>> HandleAsync(JoinGameRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        var code = InviteCode.Create(request.InviteCode);
        return await TryJoinAsync(code, userId, cancellationToken) ?? await ReevaluateAsync(code, userId, cancellationToken);
    }

    // The outcome when the game cannot or need not be joined; null when the caller can take the free seat.
    private static Result<JoinGameResponse>? Evaluate(Game game, Guid userId) => game switch
    {
        _ when game.IsPlayer(userId) => new JoinGameResponse(game.Id),
        { Phase: not GamePhase.Lobby } => GameErrors.AlreadyStarted,
        { IsFull: true } => GameErrors.Full,
        _ => null,
    };

    // Returns null when a concurrent change won the race.
    private async Task<Result<JoinGameResponse>?> TryJoinAsync(InviteCode code, Guid userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var game = await db.Games.Include(candidate => candidate.Players)
            .FirstOrDefaultAsync(candidate => candidate.InviteCode == code, cancellationToken);
        if (game is null)
        {
            return GameErrors.InviteNotFound;
        }

        if (Evaluate(game, userId) is { } outcome)
        {
            return outcome;
        }

        game.Join(userId, timeProvider.GetUtcNow());
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
        catch (DbUpdateException exception) when (dbExceptions.IsUniqueViolation(exception))
        {
            return null;
        }

        return new JoinGameResponse(game.Id);
    }

    private async Task<Result<JoinGameResponse>> ReevaluateAsync(InviteCode code, Guid userId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var game = await db.Games.AsNoTracking().Include(candidate => candidate.Players)
            .FirstOrDefaultAsync(candidate => candidate.InviteCode == code, cancellationToken);

        if (game is null)
        {
            return GameErrors.InviteNotFound;
        }

        // A seat that looks free again after a lost race is not retried: the caller may simply join again.
        return Evaluate(game, userId) ?? GameErrors.Full;
    }
}
