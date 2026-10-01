using Govern.Core.Economy;
using Govern.Core.Map;

namespace Govern.Core.Engine;

// What BuildImprovementRule.Check needs to know about the builder's tile; the rule builds it from the state, the preview from
// the view. InOwnWorkArea is whether the tile is in a work area of one of the builder owner's cities.
internal sealed record ImprovementSite(TerrainType Terrain, ImprovementType? Existing, bool InOwnWorkArea);
