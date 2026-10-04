using System.Text.Json.Serialization;
using Govern.Core.Actions;

namespace Govern.Core.Engine;

/// <summary>
/// A player's intent, sent by the client and validated by <c>GameEngine.Execute</c>. The JSON <c>type</c> discriminators are
/// part of the wire and storage format: never rename one.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MoveUnitCommand), "move_unit")]
[JsonDerivedType(typeof(SetDestinationCommand), "set_destination")]
[JsonDerivedType(typeof(ExploreCommand), "explore")]
[JsonDerivedType(typeof(CancelJobCommand), "cancel_job")]
[JsonDerivedType(typeof(AttackCommand), "attack")]
[JsonDerivedType(typeof(FoundCityCommand), "found_city")]
[JsonDerivedType(typeof(SetProductionCommand), "set_production")]
[JsonDerivedType(typeof(BuildImprovementCommand), "build_improvement")]
[JsonDerivedType(typeof(BuildBridgeCommand), "build_bridge")]
[JsonDerivedType(typeof(ProduceBuildingCommand), "produce_building")]
[JsonDerivedType(typeof(SetUpkeepPriorityCommand), "set_upkeep_priority")]
[JsonDerivedType(typeof(SetMarketOrderCommand), "set_market_order")]
[JsonDerivedType(typeof(OpenTradeRouteCommand), "open_trade_route")]
[JsonDerivedType(typeof(ProposeDealCommand), "propose_deal")]
[JsonDerivedType(typeof(AnswerDealCommand), "answer_deal")]
[JsonDerivedType(typeof(CancelDealCommand), "cancel_deal")]
[JsonDerivedType(typeof(CloseTradeRouteCommand), "close_trade_route")]
[JsonDerivedType(typeof(UnlockNodeCommand), "unlock_node")]
[JsonDerivedType(typeof(UpgradeUnitCommand), "upgrade_unit")]
[JsonDerivedType(typeof(UpgradeBuildingCommand), "upgrade_building")]
[JsonDerivedType(typeof(EndTurnCommand), "end_turn")]
[JsonDerivedType(typeof(ResignCommand), "resign")]
public abstract record GameCommand;
