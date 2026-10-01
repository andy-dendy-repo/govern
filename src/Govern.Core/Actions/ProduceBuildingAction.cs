using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Makes a city produce a building; accumulated progress is kept, and the city goes back to its unit when the building is done.
/// </summary>
/// <param name="CityId">The actor's city.</param>
/// <param name="Building">The building: a building id of the game's ruleset.</param>
public sealed record ProduceBuildingCommand(CityId CityId, BuildingType Building) : GameCommand;

/// <summary>
/// Switches a city to producing a building it doesn't have yet, keeping the accumulated progress. A building that requires an
/// adjacent terrain needs one of those terrains next to the city.
/// </summary>
internal static class ProduceBuildingRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, ProduceBuildingCommand command)
    {
        var city = state.FindCity(command.CityId);
        if (city is null || city.Owner != actor)
        {
            return RuleViolations.CityNotFound;
        }

        var neighbors = city.Position.Neighbors().Where(state.Map.Contains).Select(coord => state.Map.TileAt(coord).Terrain);
        if (Check(ruleset, city, command.Building, [.. neighbors]) is { } violation)
        {
            return violation;
        }

        var changed = city with { ProducingBuilding = command.Building };
        return new GameUpdate(state.WithCity(changed), [new ProductionChangedEvent(city.Id, city.Producing, command.Building)]);
    }

    // Shared with the view-based preview (ViewPreviewCalculator): a city sees its neighbours (city sight is at least 1).
    public static RuleViolation? Check(Ruleset ruleset, City city, BuildingType building, ImmutableArray<TerrainType> neighborTerrains)
    {
        if (ruleset.FindBuilding(building) is not { } definition)
        {
            return RuleViolations.UnknownBuilding;
        }

        if (city.Has(building))
        {
            return RuleViolations.BuildingExists;
        }

        return definition.RequiresAdjacentTerrain.IsEmpty || definition.RequiresAdjacentTerrain.Any(neighborTerrains.Contains)
            ? null
            : RuleViolations.BuildingNeedsTerrain;
    }
}
