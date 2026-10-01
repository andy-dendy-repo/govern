using Govern.Core.Map;

namespace Govern.Core.Engine;

// What BuildBridgeRule.Check needs to know about the edge between the unit's tile and the target; the rule builds it from the
// state, the preview from the view. IsNeighbor is whether the target is a neighbour on the map; River the river on the edge (if
// any); Bridged whether a bridge already spans it; TargetPassable whether units could enter the far side; TouchesEnemyTerritory
// whether either tile is in an enemy city's territory.
internal sealed record BridgeSite(bool IsNeighbor, RiverType? River, bool Bridged, bool TargetPassable, bool TouchesEnemyTerritory);
