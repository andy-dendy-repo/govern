using System.Collections.Immutable;
using Govern.Core.Map;
using Govern.Core.Units;

namespace Govern.Core.Rulesets;

// The ruleset JSON as written, before validation.
internal sealed record RulesetDocument(
    SetupRules Setup,
    MapRules Map,
    ImmutableArray<TerrainDefinition> Terrains,
    ImmutableArray<UnitDefinition> Units,
    CityRules Cities,
    CombatRules Combat,
    ImmutableDictionary<string, string> Texts);
