namespace Govern.Core.Engine;

/// <summary>
/// Every rule violation the engine returns. Codes are stable; never rename one silently.
/// </summary>
public static class RuleViolations
{
    /// <summary>The game has already finished.</summary>
    public static readonly RuleViolation GameFinished = new("game.finished", "The game has finished.");

    /// <summary>The actor is not an active player of this game.</summary>
    public static readonly RuleViolation NotAPlayer = new("game.not_a_player", "You are not an active player in this game.");

    /// <summary>It is another player's turn.</summary>
    public static readonly RuleViolation NotYourTurn = new("game.not_your_turn", "It is not your turn.");

    /// <summary>The actor has no unit with that id (it does not exist or belongs to someone else; the two are not distinguished).</summary>
    public static readonly RuleViolation UnitNotFound = new("unit.not_found", "You have no such unit.");

    /// <summary>The unit has no movement points left this turn.</summary>
    public static readonly RuleViolation NoMovementLeft = new("unit.no_movement", "The unit has no movement left this turn.");

    /// <summary>The unit type cannot found cities.</summary>
    public static readonly RuleViolation CannotFoundCity = new("unit.cannot_found_city", "This unit cannot found a city.");

    /// <summary>The unit type cannot attack.</summary>
    public static readonly RuleViolation CannotAttack = new("unit.cannot_attack", "This unit cannot attack.");

    /// <summary>A visible unit stands on the target tile (hidden units make the tile <see cref="Unreachable"/> instead).</summary>
    public static readonly RuleViolation TileOccupied = new("move.occupied", "Another unit stands on that tile.");

    /// <summary>The target tile is off the map, the unit's own tile, blocked or too far for the unit's remaining movement.</summary>
    public static readonly RuleViolation Unreachable = new("move.unreachable", "The unit cannot reach that tile this turn.");

    /// <summary>
    /// A destination is off the map, the unit's own tile, unexplored, impassable, or has no path over the tiles the player knows.
    /// </summary>
    public static readonly RuleViolation NoPath = new("move.no_path", "The unit has no known way to that tile.");

    /// <summary>The tile already holds a city.</summary>
    public static readonly RuleViolation CityTileTaken = new("city.tile_taken", "A city already stands on this tile.");

    /// <summary>Another city is closer than the minimum spacing.</summary>
    public static readonly RuleViolation CityTooClose = new("city.too_close", "Another city is too close.");

    /// <summary>The settler's tile is in a city's territory.</summary>
    public static readonly RuleViolation CityInTerritory = new("city.in_territory", "This tile already belongs to a city.");

    /// <summary>The actor has no city with that id (it does not exist or belongs to someone else; the two are not distinguished).</summary>
    public static readonly RuleViolation CityNotFound = new("city.not_found", "You have no such city.");

    /// <summary>The upkeep order is not one the game knows.</summary>
    public static readonly RuleViolation UnknownUpkeepPriority = new("city.unknown_upkeep_priority", "That upkeep order does not exist.");

    /// <summary>The unit type is not defined.</summary>
    public static readonly RuleViolation UnknownUnitType = new("city.unknown_unit_type", "That unit type does not exist.");

    /// <summary>The attack target is not adjacent to the attacker.</summary>
    public static readonly RuleViolation NotAdjacent = new("attack.not_adjacent", "The target is not next to the unit.");

    /// <summary>The target is farther than a ranged unit's range.</summary>
    public static readonly RuleViolation OutOfRange = new("attack.out_of_range", "The target is out of the unit's range.");

    /// <summary>There is no enemy unit or city on the target tile, or a ranged unit targets a city with no unit on it.</summary>
    public static readonly RuleViolation NoTarget = new("attack.no_target", "There is nothing to attack on that tile.");

    /// <summary>A melee unit would capture a city across a river it can't cross.</summary>
    public static readonly RuleViolation RiverInTheWay = new("attack.river_in_the_way", "A river the unit can't cross is in the way.");

    /// <summary>The target belongs to the attacker.</summary>
    public static readonly RuleViolation OwnTarget = new("attack.own_target", "You cannot attack your own unit or city.");

    /// <summary>The unit type cannot build tile improvements.</summary>
    public static readonly RuleViolation CannotBuildImprovements = new("unit.cannot_build_improvements", "This unit cannot build improvements.");

    /// <summary>The unit type cannot build bridges.</summary>
    public static readonly RuleViolation CannotBuildBridges = new("unit.cannot_build_bridges", "This unit cannot build bridges.");

    /// <summary>The unit is already building exactly this; starting again would throw its progress away.</summary>
    public static readonly RuleViolation AlreadyBuilding = new("build.already_building", "The unit is already building this.");

    /// <summary>The improvement is not defined.</summary>
    public static readonly RuleViolation UnknownImprovement = new("build.unknown_improvement", "That improvement does not exist.");

    /// <summary>The unit's tile is not in the territory of one of the actor's cities, or is a city's tile.</summary>
    public static readonly RuleViolation NotInTerritory = new("build.not_in_territory", "Improvements can only be built in your cities' territory, off the city tiles.");

    /// <summary>The improvement can't be built on the tile's terrain.</summary>
    public static readonly RuleViolation WrongTerrain = new("build.wrong_terrain", "That improvement can't be built on this terrain.");

    /// <summary>The tile already has an improvement.</summary>
    public static readonly RuleViolation TileImproved = new("build.tile_improved", "This tile already has an improvement.");

    /// <summary>The bridge target is not a neighbour of the unit's tile on the map.</summary>
    public static readonly RuleViolation BridgeNotAdjacent = new("bridge.not_adjacent", "A bridge must start at the unit's tile and reach a neighbouring tile.");

    /// <summary>There is no river on the edge to bridge.</summary>
    public static readonly RuleViolation NoRiver = new("bridge.no_river", "There is no river to bridge there.");

    /// <summary>The edge already has a bridge.</summary>
    public static readonly RuleViolation BridgeExists = new("bridge.exists", "A bridge already spans that river.");

    /// <summary>Units could not enter the far side of the river, so a bridge leads nowhere.</summary>
    public static readonly RuleViolation BridgeLeadsNowhere = new("bridge.impassable", "The other side of the river can't be entered.");

    /// <summary>One of the bridge's ends is in an enemy city's territory.</summary>
    public static readonly RuleViolation EnemyTerritory = new("bridge.enemy_territory", "You can't build a bridge in an enemy city's territory.");

    /// <summary>The building is not defined.</summary>
    public static readonly RuleViolation UnknownBuilding = new("city.unknown_building", "That building does not exist.");

    /// <summary>The city already has the building.</summary>
    public static readonly RuleViolation BuildingExists = new("city.building_exists", "The city already has this building.");

    /// <summary>The building needs a terrain next to its tile that is not there.</summary>
    public static readonly RuleViolation BuildingNeedsTerrain = new("city.building_needs_terrain", "This building needs a different terrain next to its tile.");

    /// <summary>The building's tile is not in the city's territory, or is the city's own tile.</summary>
    public static readonly RuleViolation BuildingNotInTerritory = new("city.building_not_in_territory", "A building must stand on a tile of the city's territory, off the city tile.");

    /// <summary>A building can't stand on the tile's terrain (it is impassable).</summary>
    public static readonly RuleViolation BuildingWrongTerrain = new("city.building_wrong_terrain", "A building can't stand on this terrain.");

    /// <summary>The building's tile already has an improvement or a building, or is reserved for another building.</summary>
    public static readonly RuleViolation BuildingSiteTaken = new("city.building_site_taken", "This tile already has an improvement or a building.");

    /// <summary>One of the actor's units is building an improvement on the building's tile.</summary>
    public static readonly RuleViolation BuildingSiteBeingImproved = new("city.building_site_being_improved", "A unit is building an improvement on this tile.");

    /// <summary>A building stands on the tile, or a city is producing one there.</summary>
    public static readonly RuleViolation TileHasBuilding = new("build.tile_has_building", "A building stands, or is being produced, on this tile.");
}
