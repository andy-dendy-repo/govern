namespace Govern.Client.Features.Games.Models;

/// <summary>
/// One choice of the lobby's map picker (<see cref="Services.MapPoolService.OptionsFor"/>).
/// </summary>
/// <param name="Value">What <c>POST /api/games</c> takes: <c>random</c>, <c>generated</c> or a pool map's key.</param>
/// <param name="Name">What players see.</param>
public sealed record MapOption(string Value, string Name);
