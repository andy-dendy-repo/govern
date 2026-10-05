using System.Collections.Immutable;

namespace Govern.Core.Economy;

/// <summary>
/// What a trade deal swaps (G17b): every turn the deal is active, its home city gives <see cref="Gives"/> to the host city and
/// the host gives <see cref="Receives"/> back, at the end of the host owner's turn.
/// </summary>
/// <param name="Gives">What the home city gives each turn: tradable resources, each once, in the ruleset's order.</param>
/// <param name="Receives">What the host city gives back each turn: tradable resources, each once, in the ruleset's order.</param>
public sealed record TradeTerms(ImmutableArray<ResourceAmount> Gives, ImmutableArray<ResourceAmount> Receives);
