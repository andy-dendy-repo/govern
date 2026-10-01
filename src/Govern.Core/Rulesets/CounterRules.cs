namespace Govern.Core.Rulesets;

/// <summary>
/// A class counter (an entry of <c>combat.counters</c>): units of <paramref name="Class"/> get <paramref name="Bonus"/> strength
/// in any fight against units of <paramref name="Against"/>, attacking or defending.
/// </summary>
/// <param name="Class">The class that gets the bonus (<see cref="Units.UnitDefinition.Class"/>).</param>
/// <param name="Against">The class it is strong against.</param>
/// <param name="Bonus">Extra strength points (at least 1).</param>
public sealed record CounterRules(string Class, string Against, int Bonus);
