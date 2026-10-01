using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Core.Engine;

// What ProduceBuildingRule.CheckSite needs to know about the tile a city's building would stand on; the rule builds it from the
// state, the preview from the view. InCityTerritory is whether the tile is on the map, in the city's territory and not the city's
// own tile; Passable whether units could stand on its terrain; Taken whether it has an improvement or a building; BeingImproved
// whether one of the owner's units is building an improvement there; NeighborTerrains the terrains next to it.
internal sealed record BuildingSite(bool InCityTerritory, bool Passable, bool Taken, bool BeingImproved, ImmutableArray<TerrainType> NeighborTerrains)
{
    // A tile off the map, which no building can take.
    public static BuildingSite Outside { get; } = new(InCityTerritory: false, Passable: false, Taken: false, BeingImproved: false, NeighborTerrains: []);
}
