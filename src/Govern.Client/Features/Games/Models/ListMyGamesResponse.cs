namespace Govern.Client.Features.Games.Models;

/// <summary>
/// A page of the caller's games, from <c>GET /api/games</c>.
/// </summary>
/// <param name="Items">The games on this page.</param>
/// <param name="Total">How many games the caller has in all.</param>
public sealed record ListMyGamesResponse(IReadOnlyList<MyGameItem> Items, int Total);
