using System.Text.Json.Serialization;

namespace Govern.Core.Events;

/// <summary>
/// Something that happened when a command was applied, for the game log and client animations. Events describe the full state,
/// so hosts filter them by visibility before sending them to a player. The JSON <c>type</c> discriminators are stable.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UnitMovedEvent), "unit_moved")]
[JsonDerivedType(typeof(UnitOrderEndedEvent), "unit_order_ended")]
[JsonDerivedType(typeof(UnitCreatedEvent), "unit_created")]
[JsonDerivedType(typeof(UnitDestroyedEvent), "unit_destroyed")]
[JsonDerivedType(typeof(UnitAttackedEvent), "unit_attacked")]
[JsonDerivedType(typeof(CityFoundedEvent), "city_founded")]
[JsonDerivedType(typeof(CityCapturedEvent), "city_captured")]
[JsonDerivedType(typeof(ProductionChangedEvent), "production_changed")]
[JsonDerivedType(typeof(TurnEndedEvent), "turn_ended")]
[JsonDerivedType(typeof(BuildStartedEvent), "build_started")]
[JsonDerivedType(typeof(ImprovementBuiltEvent), "improvement_built")]
[JsonDerivedType(typeof(BridgeBuiltEvent), "bridge_built")]
[JsonDerivedType(typeof(BuildingCompletedEvent), "building_completed")]
[JsonDerivedType(typeof(TerritoryGrewEvent), "territory_grew")]
[JsonDerivedType(typeof(UnitStarvedEvent), "unit_starved")]
[JsonDerivedType(typeof(BuildingDamagedEvent), "building_damaged")]
[JsonDerivedType(typeof(UpkeepPrioritySetEvent), "upkeep_priority_set")]
[JsonDerivedType(typeof(TradeRouteChangedEvent), "trade_route_changed")]
[JsonDerivedType(typeof(TradeExchangedEvent), "trade_exchanged")]
[JsonDerivedType(typeof(ScienceNodeChangedEvent), "science_node_changed")]
[JsonDerivedType(typeof(UnitUpgradedEvent), "unit_upgraded")]
[JsonDerivedType(typeof(BuildingUpgradedEvent), "building_upgraded")]
[JsonDerivedType(typeof(PlayerResignedEvent), "player_resigned")]
[JsonDerivedType(typeof(PlayerEliminatedEvent), "player_eliminated")]
[JsonDerivedType(typeof(GameWonEvent), "game_won")]
public abstract record GameEvent;
