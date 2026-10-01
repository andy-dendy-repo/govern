namespace Govern.Core.Cities;

/// <summary>Which a city pays first when its stock can't keep up everything on its territory.</summary>
public enum UpkeepPriority
{
    /// <summary>Its buildings (in the ruleset's order), then its units (lowest health first).</summary>
    BuildingsFirst,

    /// <summary>Its units (lowest health first), then its buildings (in the ruleset's order).</summary>
    UnitsFirst,
}
