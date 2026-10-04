using System.Collections.Immutable;
using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Science;
using Govern.Core.State;

namespace Govern.Core.Actions;

/// <summary>
/// Makes a city produce a building on a tile of its territory; accumulated progress is kept, and the city goes back to its unit
/// when the building is done. A resource cost already paid for the item it leaves goes back to the city's stock.
/// </summary>
/// <param name="CityId">The actor's city.</param>
/// <param name="Building">The building: a building id of the game's ruleset.</param>
/// <param name="Site">The tile the building will stand on, reserved while the city produces it.</param>
public sealed record ProduceBuildingCommand(CityId CityId, BuildingType Building, HexCoord Site) : GameCommand;

/// <summary>
/// Switches a city to producing a building it doesn't have yet (in any version), that is unlocked and isn't an upgrade, keeping the accumulated progress, and reserves the building's
/// tile. The tile must be in the city's territory (off the city tile), passable, free of improvements and buildings and of an
/// improvement being built; a building that requires an adjacent terrain needs one of those terrains next to the tile. Ordering
/// the same building on another tile only moves the reservation, and on the same tile changes nothing.
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

        if (Check(ruleset, state.FindPlayer(actor)!.NodeList, city, command.Building) is { } violation)
        {
            return violation;
        }

        if (CheckSite(ruleset.Building(command.Building), SiteOf(ruleset, state, city, command.Site)) is { } siteViolation)
        {
            return siteViolation;
        }

        if (city.ProducingBuilding == command.Building && city.ProducingBuildingSite == command.Site)
        {
            return new GameUpdate(state, []);
        }

        var changed = city.ProducingBuilding == command.Building
            ? city with { ProducingBuildingSite = command.Site }
            : CityProduction.Refund(ruleset, city) with { ProducingBuilding = command.Building, ProducingBuildingSite = command.Site };
        return new GameUpdate(state.WithCity(changed), [new ProductionChangedEvent(city.Id, city.Producing, command.Building)]);
    }

    // Whether the city may produce the building at all, wherever it stands; shared with the view-based preview.
    // A city never produces an upgrade (a builder makes it), nor a building it has in any version, nor a locked one.
    public static RuleViolation? Check(Ruleset ruleset, ImmutableArray<OwnedNode> nodes, City city, BuildingType building)
    {
        if (ruleset.FindBuilding(building) is not { } definition)
        {
            return RuleViolations.UnknownBuilding;
        }

        if (definition.Upgrades is not null)
        {
            return RuleViolations.BuildingIsUpgrade;
        }

        if (city.BuildingList.Any(built => ruleset.IsKindOf(built, building)))
        {
            return RuleViolations.BuildingExists;
        }

        return ScienceCalculator.IsUnlocked(ruleset, nodes, building) ? null : RuleViolations.Locked;
    }

    // The site checks in the order CheckSite makes them; a later one means the site passed the earlier ones. The view preview
    // reads the order to name the site that came closest when none is accepted.
    private static readonly ImmutableArray<(Func<BuildingDefinition, BuildingSite, bool> Fails, RuleViolation Violation)> SiteChecks =
    [
        (static (_, site) => !site.InCityTerritory, RuleViolations.BuildingNotInTerritory),
        (static (_, site) => !site.Passable, RuleViolations.BuildingWrongTerrain),
        (static (_, site) => site.Taken, RuleViolations.BuildingSiteTaken),
        (static (_, site) => site.BeingImproved, RuleViolations.BuildingSiteBeingImproved),
        (static (building, site) => !building.RequiresAdjacentTerrain.IsEmpty && !building.RequiresAdjacentTerrain.Any(site.NeighborTerrains.Contains),
            RuleViolations.BuildingNeedsTerrain),
    ];

    public static ImmutableArray<RuleViolation> SiteCheckOrder { get; } = [.. SiteChecks.Select(static check => check.Violation)];

    // Whether the building may stand on the site; shared with the view-based preview (ViewPreviewCalculator).
    public static RuleViolation? CheckSite(BuildingDefinition building, BuildingSite site) =>
        SiteChecks.FirstOrDefault(check => check.Fails(building, site)).Violation;

    private static BuildingSite SiteOf(Ruleset ruleset, GameState state, City city, HexCoord coord)
    {
        if (!state.Map.Contains(coord))
        {
            return BuildingSite.Outside;
        }

        var tile = state.Map.TileAt(coord);
        var improving = state.UnitAt(coord) is { Job.Improvement: not null } unit && unit.Owner == city.Owner;
        var neighbors = coord.Neighbors().Where(state.Map.Contains).Select(neighbor => state.Map.TileAt(neighbor).Terrain);
        return new BuildingSite(
            tile.Territory == city.Id && coord != city.Position,
            ruleset.Terrain(tile.Terrain).IsPassable,
            tile.Improvement is not null || tile.Building is not null,
            improving,
            [.. neighbors]);
    }
}
