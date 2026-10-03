using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Engine;
using Govern.Core.Events;
using Govern.Core.Rulesets;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Actions;

/// <summary>
/// Starts building an improvement on the unit's tile, which must be in the territory of one of the actor's cities (not a city tile).
/// </summary>
/// <param name="UnitId">The actor's builder.</param>
/// <param name="Improvement">The improvement: an improvement id of the game's ruleset.</param>
public sealed record BuildImprovementCommand(UnitId UnitId, ImprovementType Improvement) : GameCommand;

/// <summary>
/// Gives a builder the job of building an improvement on its tile (see <see cref="UnitJob"/>). The tile must be in one of the
/// actor's cities' territory (off the city tiles), have a terrain the improvement allows, no improvement yet and no building (built
/// or reserved by a city producing one). Starting uses the unit's remaining
/// movement and replaces any job it had.
/// </summary>
internal static class BuildImprovementRule
{
    public static CommandResult Apply(Ruleset ruleset, GameState state, PlayerId actor, BuildImprovementCommand command)
    {
        if (!CommandGuardRule.TryGetOwnedUnit(state, actor, command.UnitId, out var unit, out var error))
        {
            return error;
        }

        if (Check(ruleset, unit, command.Improvement, SiteOf(ruleset, state, unit)) is { } violation)
        {
            return violation;
        }

        var job = UnitJob.ForImprovement(command.Improvement, state.Turn);
        return new GameUpdate(state.WithUnit(unit with { MovementLeft = 0, Job = job, Destination = null }), [new BuildStartedEvent(unit.Id, job)]);
    }

    // Shared with the view-based preview (ViewPreviewCalculator): the unit's own tile is always visible, and so are all of the
    // viewer's cities.
    public static RuleViolation? Check(Ruleset ruleset, Unit unit, ImprovementType improvement, ImprovementSite site)
    {
        if (!ruleset.Unit(unit.Type).CanBuildImprovements)
        {
            return RuleViolations.CannotBuildImprovements;
        }

        if (ruleset.FindImprovement(improvement) is not { } definition)
        {
            return RuleViolations.UnknownImprovement;
        }

        if (unit.MovementLeft <= 0)
        {
            return RuleViolations.NoMovementLeft;
        }

        return unit.Job?.Improvement == improvement ? RuleViolations.AlreadyBuilding : CheckPlacement(definition, site);
    }

    // Whether the improvement may stand on the site; also checked again each turn while the job lasts.
    public static RuleViolation? CheckPlacement(ImprovementDefinition improvement, ImprovementSite site)
    {
        if (!site.InOwnTerritory)
        {
            return RuleViolations.NotInTerritory;
        }

        if (site.Existing is not null)
        {
            return RuleViolations.TileImproved;
        }

        if (site.HasBuilding)
        {
            return RuleViolations.TileHasBuilding;
        }

        return improvement.Terrains.Contains(site.Terrain) ? null : RuleViolations.WrongTerrain;
    }

    public static ImprovementSite SiteOf(Ruleset ruleset, GameState state, Unit unit)
    {
        var tile = state.Map.TileAt(unit.Position);
        var inTerritory = tile.Territory is { } territory
            && state.FindCity(territory)?.Owner == unit.Owner
            && state.CityAt(unit.Position) is null;
        var hasBuilding = tile.Building is not null || state.Cities.Any(city => city.ProducingBuildingSite == unit.Position);
        return new ImprovementSite(tile.Terrain, tile.Improvement, inTerritory, hasBuilding);
    }
}
