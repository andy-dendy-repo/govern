namespace Govern.Core.Rulesets;

/// <summary>
/// Upkeep (the ruleset's <c>upkeep</c> section): how units outside their territory are supplied, what a shortage costs, and the
/// health of city buildings.
/// </summary>
/// <param name="SupplyRadius">A unit outside its own territory is supplied by the tiles up to this distance (its own included).</param>
/// <param name="ShortageDamage">Health a unit or building loses in a turn its upkeep isn't paid; a unit never drops below 1.</param>
/// <param name="BuildingHealth">A building's full health; at 0 it gives nothing until it recovers.</param>
/// <param name="BuildingRecovery">Health a hurt building regains in a turn its upkeep is paid.</param>
public sealed record UpkeepRules(int SupplyRadius, int ShortageDamage, int BuildingHealth, int BuildingRecovery);
