using System.Text.Json.Serialization;

namespace Govern.Core.Events;

/// <summary>
/// Something that happened when a command was applied, for the game log and client animations. Events describe the full state,
/// so hosts filter them by visibility before sending them to a player. The JSON <c>type</c> discriminators are stable.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UnitMovedEvent), "unit_moved")]
[JsonDerivedType(typeof(UnitCreatedEvent), "unit_created")]
[JsonDerivedType(typeof(UnitDestroyedEvent), "unit_destroyed")]
[JsonDerivedType(typeof(CityFoundedEvent), "city_founded")]
[JsonDerivedType(typeof(CityCapturedEvent), "city_captured")]
[JsonDerivedType(typeof(ProductionChangedEvent), "production_changed")]
[JsonDerivedType(typeof(TurnEndedEvent), "turn_ended")]
[JsonDerivedType(typeof(PlayerResignedEvent), "player_resigned")]
[JsonDerivedType(typeof(PlayerEliminatedEvent), "player_eliminated")]
[JsonDerivedType(typeof(GameWonEvent), "game_won")]
public abstract record GameEvent;
