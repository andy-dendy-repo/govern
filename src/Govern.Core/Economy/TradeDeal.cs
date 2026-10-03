using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Govern.Core.Economy;

/// <summary>
/// The swap a trade route carries (G17): every turn the route's home city gives <see cref="Gives"/> to the host city and the host
/// gives <see cref="Receives"/> back, at the end of the host owner's turn, while the deal is accepted and its trader is on its way.
/// </summary>
/// <param name="Gives">What the home city gives each turn: tradable resources, each once, in the ruleset's order.</param>
/// <param name="Receives">What the host city gives back each turn: tradable resources, each once, in the ruleset's order.</param>
/// <param name="Accepted">Whether the host accepted it; until then it is a proposal the host answers on its turn.</param>
/// <param name="AcceptedTurn">The round it was accepted in; older deals trade first.</param>
public sealed record TradeDeal(
    ImmutableArray<ResourceAmount> Gives,
    ImmutableArray<ResourceAmount> Receives,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Accepted = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int AcceptedTurn = 0);
