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

    /// <summary>
    /// A new city's name is empty, longer than <see cref="Actions.FoundCityCommand.MaxNameLength"/> characters or uses a character
    /// other than letters, digits, spaces and <c>' - .</c>.
    /// </summary>
    public static readonly RuleViolation CityNameInvalid = new("city.name_invalid", "Use 1 to 24 letters, digits, spaces, apostrophes, hyphens or dots.");

    /// <summary>Another of the actor's cities already has that name (ignoring case).</summary>
    public static readonly RuleViolation CityNameTaken = new("city.name_taken", "One of your cities already has this name.");

    /// <summary>The actor has no city with that id (it does not exist or belongs to someone else; the two are not distinguished).</summary>
    public static readonly RuleViolation CityNotFound = new("city.not_found", "You have no such city.");

    /// <summary>The upkeep order is not one the game knows.</summary>
    public static readonly RuleViolation UnknownUpkeepPriority = new("city.unknown_upkeep_priority", "That upkeep order does not exist.");

    /// <summary>The unit is produced only in a city with a certain building (or an upgrade of it), such as a ship in a harbour.</summary>
    public static readonly RuleViolation MissingBuilding = new("city.missing_building", "The city needs the building this unit is produced in.");

    /// <summary>The unit type is not defined.</summary>
    public static readonly RuleViolation UnknownUnitType = new("city.unknown_unit_type", "That unit type does not exist.");

    /// <summary>The attack target is not adjacent to the attacker.</summary>
    public static readonly RuleViolation NotAdjacent = new("attack.not_adjacent", "The target is not next to the unit.");

    /// <summary>The target is farther than a ranged unit's range.</summary>
    public static readonly RuleViolation OutOfRange = new("attack.out_of_range", "The target is out of the unit's range.");

    /// <summary>
    /// A melee attack on a unit of the other domain (a land unit attacking a ship, or a ship attacking a land unit or a city):
    /// melee only reaches the same ground; ranged attacks reach both.
    /// </summary>
    public static readonly RuleViolation OtherDomain = new("attack.other_domain", "Melee attacks only reach units on the same ground: land against land, ships against ships.");

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

    /// <summary>The unit type cannot trade.</summary>
    public static readonly RuleViolation CannotTrade = new("trade.cannot_trade", "This unit cannot trade.");

    /// <summary>The trader serves a trade route; its owner can only free it by closing the route.</summary>
    public static readonly RuleViolation OnTradeRoute = new("trade.on_route", "The trader serves a trade route; close the route to use it.");

    /// <summary>The city has no trade post (the ruleset's <c>trade.building</c>) standing on a tile of its territory.</summary>
    public static readonly RuleViolation NoTradePost = new("trade.no_trade_post", "The city needs a Trade post to trade.");

    /// <summary>
    /// The target is the home city itself, or a city the actor doesn't see, or one whose trade post the actor doesn't see (the
    /// three are not distinguished, so probing reveals nothing hidden).
    /// </summary>
    public static readonly RuleViolation NoTradePartner = new("trade.no_partner", "Pick another city whose Trade post you can see.");

    /// <summary>The home city already opened as many routes as <c>trade.routesPerCity</c> allows.</summary>
    public static readonly RuleViolation TooManyRoutes = new("trade.too_many_routes", "This city already runs as many trade routes as it can.");

    /// <summary>A route between the two cities already has its trader.</summary>
    public static readonly RuleViolation RouteHasTrader = new("trade.route_has_trader", "A trader already serves the route between these cities.");

    /// <summary>The route does not exist, or the actor is not the side the command needs (the two are not distinguished).</summary>
    public static readonly RuleViolation RouteNotFound = new("trade.route_not_found", "You have no such trade route.");

    /// <summary>The route's trader is not at the host's trade post (or within one tile of it), or there is none.</summary>
    public static readonly RuleViolation TraderNotAtPost = new("trade.trader_not_at_post", "The trader must stand at the host's Trade post.");

    /// <summary>The route already carries an accepted deal.</summary>
    public static readonly RuleViolation DealActive = new("trade.deal_active", "Cancel the current deal first.");

    /// <summary>The resource doesn't exist or can't be traded.</summary>
    public static readonly RuleViolation NotTradable = new("trade.not_tradable", "That resource can't be traded.");

    /// <summary>One side of a deal gives nothing.</summary>
    public static readonly RuleViolation EmptyDeal = new("trade.empty_deal", "Each side of a deal must give something.");

    /// <summary>An amount is out of range, or a resource is listed twice on one side.</summary>
    public static readonly RuleViolation BadTradeAmount = new("trade.bad_amount", "Each amount must be from 1 to the largest cap, each resource once.");

    /// <summary>The home city's market doesn't sell or buy that much of a resource.</summary>
    public static readonly RuleViolation HomeMarketFull = new("trade.home_market", "Your city's market doesn't sell or buy that much.");

    /// <summary>The host city's market doesn't sell or buy that much of a resource.</summary>
    public static readonly RuleViolation HostMarketFull = new("trade.host_market", "The host city's market doesn't sell or buy that much.");

    /// <summary>There is no proposal on the route to answer.</summary>
    public static readonly RuleViolation NoProposal = new("trade.no_proposal", "There is no proposal to answer.");

    /// <summary>There is no deal on the route to cancel.</summary>
    public static readonly RuleViolation NoDeal = new("trade.no_deal", "There is no deal to cancel.");

    /// <summary>A deal to cancel to make room is not an accepted deal of the host city's market.</summary>
    public static readonly RuleViolation CannotCancelDeal = new("trade.cannot_cancel", "You can only cancel accepted deals of this city.");

    /// <summary>A market order's cap is outside 1 to <c>trade.maxCap</c>.</summary>
    public static readonly RuleViolation CapOutOfRange = new("market.cap_out_of_range", "The cap must be from 1 to the largest cap.");

    /// <summary>The city's deals already use more of the order than the new one allows.</summary>
    public static readonly RuleViolation CapBelowDeals = new("market.below_deals", "Your deals already use more than that; cancel one first.");

    /// <summary>The science node does not exist.</summary>
    public static readonly RuleViolation UnknownNode = new("science.unknown_node", "That science item does not exist.");

    /// <summary>The player already owns the science node.</summary>
    public static readonly RuleViolation NodeOwned = new("science.node_owned", "You already have this science item.");

    /// <summary>A node the science node requires is not owned, or is disabled.</summary>
    public static readonly RuleViolation NodeNeedsPrerequisite = new("science.needs_prerequisite", "You need the science items it builds on first.");

    /// <summary>The player's free science per turn doesn't cover the node's points.</summary>
    public static readonly RuleViolation NotEnoughScience = new("science.not_enough", "You don't have enough free science per turn.");

    /// <summary>The unit or building is unlocked by a science node the player doesn't own, or whose node is disabled.</summary>
    public static readonly RuleViolation Locked = new("science.locked", "A science item must unlock this first.");

    /// <summary>A newer version of the unit is unlocked, so cities produce that one instead.</summary>
    public static readonly RuleViolation Replaced = new("science.replaced", "A newer unit replaces this one.");

    /// <summary>The building is an upgrade: a builder upgrades it from the building before it, a city never produces it.</summary>
    public static readonly RuleViolation BuildingIsUpgrade = new("city.building_is_upgrade", "A builder upgrades the building before it into this one.");

    /// <summary>The unit, or the building on the builder's tile, has no unlocked upgrade.</summary>
    public static readonly RuleViolation NoUpgrade = new("upgrade.none", "There is no upgrade to make.");

    /// <summary>The unit, or the building, is not on a tile of one of the actor's cities' territory.</summary>
    public static readonly RuleViolation UpgradeNotInTerritory = new("upgrade.not_in_territory", "Upgrades are made on your cities' territory.");

    /// <summary>The city owning the tile lacks the resources the unit's upgrade costs.</summary>
    public static readonly RuleViolation UpgradeMissingResources = new("upgrade.missing_resources", "The city owning this tile lacks the resources for the upgrade.");
}
