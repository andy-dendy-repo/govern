using Govern.Core.Map;
using Govern.Core.Units;

namespace Govern.Core.Engine;

// One unit in a fight, as CombatCalculator needs it; built from the state (AttackRule) or from a view (the preview), so both
// work out the same numbers.
internal sealed record Combatant(UnitType Type, int Health, TerrainType Terrain);
