using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Core.Rulesets;

/// <summary>
/// How a new map gets its rivers (the ruleset's <c>map.generation.rivers</c> section). Each river starts at a corner of a
/// <see cref="Sources"/> tile, runs along tile edges away from it and ends where it reaches a <see cref="Mouths"/> tile or the
/// map's border. It gets deeper downstream (<see cref="Stages"/>). Every river is laid together with its mirror.
/// </summary>
/// <param name="Count">How many rivers to lay down (each with its mirror, so a map holds up to twice as many). 0 for none.</param>
/// <param name="MinLength">The fewest edges a river has; a shorter one is dropped.</param>
/// <param name="MaxLength">The most edges a river has; one that hasn't reached a mouth by then is dropped.</param>
/// <param name="Sources">Terrains a river may start next to (hills, mountains).</param>
/// <param name="Mouths">Terrains a river may end at (lakes); the map's border always ends a river too.</param>
/// <param name="Stages">The river kind along the river, by edge number from the source; the first stage starts at edge 0.</param>
public sealed record RiverGenerationRules(
    int Count,
    int MinLength,
    int MaxLength,
    ImmutableArray<TerrainType> Sources,
    ImmutableArray<TerrainType> Mouths,
    ImmutableArray<RiverStageRules> Stages);
