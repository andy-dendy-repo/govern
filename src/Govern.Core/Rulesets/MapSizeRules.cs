namespace Govern.Core.Rulesets;

/// <summary>
/// The size of a generated map for a number of players (an entry of the ruleset's <c>map.sizes</c>).
/// </summary>
/// <param name="Players">The number of players this size is for.</param>
/// <param name="Width">Columns of the map.</param>
/// <param name="Height">Rows of the map.</param>
public sealed record MapSizeRules(int Players, int Width, int Height);
