using Govern.Core.Units;

namespace Govern.Core.Rulesets;

/// <summary>
/// Cities (the ruleset's <c>cities</c> section).
/// </summary>
/// <param name="Sight">Radius, in tiles, a city reveals around itself.</param>
/// <param name="MinimumSpacing">The smallest hex distance between two cities.</param>
/// <param name="ProductionPerTurn">Production points a city adds at the end of its owner's turn.</param>
/// <param name="InitialProduction">What a new city builds first.</param>
/// <param name="NameTemplate">The text key of a new city's name; <c>{n}</c> in the text is the founder's city count.</param>
/// <param name="CaptureResetsProduction">Whether a captured city's production progress goes back to 0.</param>
public sealed record CityRules(
    int Sight,
    int MinimumSpacing,
    int ProductionPerTurn,
    UnitType InitialProduction,
    string NameTemplate,
    bool CaptureResetsProduction);
