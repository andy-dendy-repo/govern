namespace Govern.Core.Map;

/// <summary>
/// Rules data for one <see cref="RiverType"/>, from the game's ruleset. A river runs along the edge between two tiles.
/// </summary>
/// <param name="Id">The river this definition describes.</param>
/// <param name="CrossingCost">Movement points a land unit spends to cross it, on top of the cost of the tile it enters (0 for none).</param>
/// <param name="IsCrossable">Whether land units may cross it at all.</param>
/// <param name="DefenceBonus">Extra strength for a unit attacked in melee across it (0 for none).</param>
public sealed record RiverDefinition(RiverType Id, int CrossingCost, bool IsCrossable, int DefenceBonus);
