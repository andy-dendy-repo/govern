using FluentValidation;
using Govern.Application.Common.Abstractions;
using Govern.Application.Games.Models;
using Govern.Application.Users.Models;
using Govern.Utils.Mediator.Abstractions;
using Govern.Utils.Results;
using Microsoft.EntityFrameworkCore;

namespace Govern.Application.Games.Contracts;

/// <summary>
/// Lists the games the caller plays in, the most recent activity both players see first.
/// </summary>
/// <param name="Page">One-based page number, 1–<see cref="ListMyGamesValidator.MaxPage"/>.</param>
/// <param name="PageSize">Items per page, 1–<see cref="ListMyGamesValidator.MaxPageSize"/>.</param>
public sealed record ListMyGamesRequest(int Page = 1, int PageSize = ListMyGamesRequest.DefaultPageSize)
    : IRequest<ListMyGamesRequest, ListMyGamesResponse>
{
    /// <summary>Page size when none is given.</summary>
    public const int DefaultPageSize = 20;
}

/// <summary>
/// One page of the caller's games.
/// </summary>
/// <param name="Items">The games on this page.</param>
/// <param name="Total">How many games the caller plays in overall.</param>
public sealed record ListMyGamesResponse(IReadOnlyList<MyGameItem> Items, int Total) : IResponse;

/// <summary>
/// Validates <see cref="ListMyGamesRequest"/>.
/// </summary>
public sealed class ListMyGamesValidator : AbstractValidator<ListMyGamesRequest>
{
    /// <summary>Largest page size.</summary>
    public const int MaxPageSize = 50;

    /// <summary>Largest page number (keeps the row offset far from overflowing).</summary>
    public const int MaxPage = 10_000;

    /// <summary>Creates the validation rules.</summary>
    public ListMyGamesValidator()
    {
        RuleFor(request => request.Page).InclusiveBetween(1, MaxPage);
        RuleFor(request => request.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}

/// <summary>
/// Handles <see cref="ListMyGamesRequest"/> with a SQL projection that never reads the game state column.
/// </summary>
internal sealed class ListMyGamesHandler(IApplicationDbContextFactory dbContextFactory, ICurrentUserService currentUser)
    : IRequestHandler<ListMyGamesRequest, ListMyGamesResponse>
{
    /// <inheritdoc />
    public async ValueTask<Result<ListMyGamesResponse>> HandleAsync(ListMyGamesRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return UserErrors.NotAuthenticated;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var games = db.Games.AsNoTracking().Where(game => game.Players.Any(player => player.UserId == userId));

        var total = await games.CountAsync(cancellationToken);
        var items = await games
            .OrderByDescending(game => game.ActivityAt)
            .ThenByDescending(game => game.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(game => new MyGameItem(
                game.Id,
                game.Phase,
                game.Players.Count,
                game.CreatorId == userId,
                game.Players.Any(player => player.UserId == userId && player.Id == game.CurrentPlayerId),
                game.ActivityAt))
            .ToListAsync(cancellationToken);

        return new ListMyGamesResponse(items, total);
    }
}
