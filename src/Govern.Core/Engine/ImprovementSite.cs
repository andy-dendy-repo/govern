using Govern.Core.Economy;
using Govern.Core.Map;

namespace Govern.Core.Engine;

// What BuildImprovementRule.Check needs to know about the builder's tile; the rule builds it from the state, the preview from
// the view. InOwnTerritory is whether the tile is in the territory of one of the builder owner's cities and is not a city's tile;
// HasBuilding whether a building stands on it or a city has reserved it for the building it produces.
internal sealed record ImprovementSite(TerrainType Terrain, ImprovementType? Existing, bool InOwnTerritory, bool HasBuilding);
