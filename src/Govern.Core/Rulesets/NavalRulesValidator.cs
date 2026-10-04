using Govern.Core.Units;

namespace Govern.Core.Rulesets;

// The naval checks of RulesetValidator: water terrain, naval units and the buildings units are produced in. Problems go into
// the same list, with JSON paths.
internal static class NavalRulesValidator
{
    // Only melee and ranged attacks make sense on water: ships don't found cities, build or trade.
    private static readonly UnitAbility[] NavalAbilities = [UnitAbility.Attack, UnitAbility.RangedAttack];

    public static void Validate(RulesetDocument document, List<RulesetProblem> problems)
    {
        ValidateTerrains(document, problems);
        ValidateUnits(document, problems);
        ValidateStart(document, problems);
    }

    // A tile is either land or water, so a step's domain check never has two answers.
    private static void ValidateTerrains(RulesetDocument document, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Terrains.Length; index++)
        {
            if (document.Terrains[index] is { IsWater: true, IsPassable: true })
            {
                problems.Add(new($"$.terrains[{index}].isWater", "A water terrain must not be passable for land units."));
            }
        }
    }

    private static void ValidateUnits(RulesetDocument document, List<RulesetProblem> problems)
    {
        var water = document.Terrains.Where(static terrain => terrain.IsWater).Select(static terrain => terrain.Id).ToHashSet();
        for (var index = 0; index < document.Units.Length; index++)
        {
            var unit = document.Units[index];
            var building = unit.RequiresBuilding is { } needed ? document.Buildings.FirstOrDefault(known => known.Id == needed) : null;
            if (unit.RequiresBuilding is { } missing && building is null)
            {
                problems.Add(new($"$.units[{index}].requiresBuilding", $"The building '{missing.Id}' does not exist."));
            }

            if (!unit.IsNaval)
            {
                continue;
            }

            if (water.Count == 0)
            {
                problems.Add(new($"$.units[{index}].domain", "A naval unit needs a water terrain (isWater) to sail."));
            }

            // A ship is launched on water next to its building, so the building must stand next to water.
            if (unit.RequiresBuilding is null)
            {
                problems.Add(new($"$.units[{index}].requiresBuilding", "A naval unit needs the building it is launched from."));
            }
            else if (building is not null && !building.RequiresAdjacentTerrain.Any(water.Contains))
            {
                problems.Add(new($"$.units[{index}].requiresBuilding", $"The building '{building.Id.Id}' must require a water terrain next to it."));
            }

            if (unit.Abilities.Any(static ability => !NavalAbilities.Contains(ability)))
            {
                problems.Add(new($"$.units[{index}].abilities", "A naval unit can only attack or shoot."));
            }
        }
    }

    // Starting units stand on land and a new city has no buildings yet, so neither may be a ship or need a building.
    private static void ValidateStart(RulesetDocument document, List<RulesetProblem> problems)
    {
        for (var index = 0; index < document.Setup.StartingUnits.Length; index++)
        {
            if (document.Units.FirstOrDefault(unit => unit.Id == document.Setup.StartingUnits[index]) is { } unit && (unit.IsNaval || unit.RequiresBuilding is not null))
            {
                problems.Add(new($"$.setup.startingUnits[{index}]", "A starting unit must be a land unit that needs no building."));
            }
        }

        if (document.Units.FirstOrDefault(unit => unit.Id == document.Cities.InitialProduction) is { } first && (first.IsNaval || first.RequiresBuilding is not null))
        {
            problems.Add(new("$.cities.initialProduction", "A new city's first unit must be a land unit that needs no building."));
        }
    }
}
