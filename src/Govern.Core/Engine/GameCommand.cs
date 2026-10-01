using System.Text.Json.Serialization;
using Govern.Core.Actions;

namespace Govern.Core.Engine;

/// <summary>
/// A player's intent, sent by the client and validated by <c>GameEngine.Execute</c>. The JSON <c>type</c> discriminators are
/// part of the wire and storage format: never rename one.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MoveUnitCommand), "move_unit")]
[JsonDerivedType(typeof(AttackCommand), "attack")]
[JsonDerivedType(typeof(FoundCityCommand), "found_city")]
[JsonDerivedType(typeof(SetProductionCommand), "set_production")]
[JsonDerivedType(typeof(BuildImprovementCommand), "build_improvement")]
[JsonDerivedType(typeof(BuildBridgeCommand), "build_bridge")]
[JsonDerivedType(typeof(ProduceBuildingCommand), "produce_building")]
[JsonDerivedType(typeof(EndTurnCommand), "end_turn")]
[JsonDerivedType(typeof(ResignCommand), "resign")]
public abstract record GameCommand;
