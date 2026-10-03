using System.Text.Json.Serialization;

namespace Govern.Core.Economy;

/// <summary>
/// Rules data for one <see cref="ResourceType"/>, from the game's ruleset. Each city keeps resources in its own
/// stock; territory growth spends one of them.
/// </summary>
/// <param name="Id">The resource this definition describes.</param>
/// <param name="TradeValue">
/// What the resource is worth in a trade at a demand level of 100 % (G17); 0 (or left out) when it can't be traded.
/// </param>
public sealed record ResourceDefinition(ResourceType Id, int TradeValue = 0)
{
    /// <summary>Whether cities can sell and buy the resource.</summary>
    [JsonIgnore]
    public bool IsTradable => TradeValue > 0;
}
