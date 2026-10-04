using Govern.Core.Economy;
using System.Collections.Immutable;
using Govern.Core.Map;
using Govern.Core.Units;

namespace Govern.Core.Rulesets;

// The ruleset JSON as written, before validation.
internal sealed record RulesetDocument(
    SetupRules Setup,
    MapRules Map,
    ImmutableArray<TerrainDefinition> Terrains,
    ImmutableArray<RiverDefinition> Rivers,
    BridgeRules Bridges,
    ImmutableArray<UnitDefinition> Units,
    CityRules Cities,
    TerritoryRules Territory,
    UpkeepRules Upkeep,
    ImmutableArray<ResourceDefinition> Resources,
    ImmutableArray<ImprovementDefinition> Improvements,
    ImmutableArray<BuildingDefinition> Buildings,
    CombatRules Combat,
    TradeRules Trade,
    ScienceRules Science,
    ImmutableDictionary<string, string> Texts);
