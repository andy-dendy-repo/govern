namespace Govern.Client.Features.Games.Models;

/// <summary>
/// The body of <c>POST /api/games</c>.
/// </summary>
/// <param name="Map">The map choice: <c>random</c>, <c>generated</c> or a pool map's key (<see cref="MapOption.Value"/>).</param>
public sealed record CreateGameRequest(string Map);
