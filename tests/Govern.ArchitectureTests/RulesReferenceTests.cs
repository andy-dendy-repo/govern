using Govern.Application.Games.Services;
using Govern.ArchitectureTests.Support;
using Govern.Core.Economy;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.ArchitectureTests;

/// <summary>
/// The player-facing rules reference (<c>docs/game/rules.md</c>) documents the ruleset the game is played with: every terrain,
/// river kind, unit, improvement and building has one row with the ruleset's numbers, every class counter has one row, and the "Game numbers" table matches the
/// setup, map, city and combat sections.
/// </summary>
public class RulesReferenceTests
{
    private const string Because = "docs/game/rules.md must match the ruleset the game is played with; update the page with the rules change";

    private static readonly string RulesFile = Path.Combine(SourceTree.RepositoryRoot, "docs", "game", "rules.md");

    private static readonly Ruleset Ruleset = new DefaultRulesetService().Ruleset;

    // The label each ability has on the page. A new ability fails the unit test until it gets one here and on the page.
    private static readonly Dictionary<UnitAbility, string> AbilityLabels = new()
    {
        [UnitAbility.FoundCity] = "Found city",
        [UnitAbility.Attack] = "Attack",
        [UnitAbility.RangedAttack] = "Ranged attack",
        [UnitAbility.BuildImprovement] = "Build improvements",
        [UnitAbility.BuildBridge] = "Build bridges",
    };

    [Fact]
    public void TerrainTable_MatchesTheRuleset()
    {
        var expected = Ruleset.Terrains
            .Select(static terrain => Row(
                Ruleset.TerrainName(terrain.Id),
                terrain.IsPassable ? $"{terrain.MovementCost}" : "—",
                YesNo(terrain.IsPassable),
                terrain.SightBonus == 0 ? "—" : $"+{terrain.SightBonus}",
                terrain.DefenceBonus == 0 ? "—" : $"+{terrain.DefenceBonus}",
                Yields(terrain.Yields)))
            .ToList();

        Table("Terrain types").ShouldBe(expected, ignoreOrder: true, Because);
    }

    [Fact]
    public void RiverTable_MatchesTheRuleset()
    {
        var expected = Ruleset.Rivers
            .Select(static river => Row(
                Ruleset.RiverName(river.Id),
                !river.IsCrossable ? "Can't cross" : river.CrossingCost == 0 ? "Free" : $"+{river.CrossingCost}",
                river.DefenceBonus == 0 ? "—" : $"+{river.DefenceBonus}",
                Yields(river.Yields)))
            .ToList();

        Table("Rivers").ShouldBe(expected, ignoreOrder: true, Because);
    }

    [Fact]
    public void UnitTable_MatchesTheRuleset()
    {
        Enum.GetValues<UnitAbility>().Where(static ability => !AbilityLabels.ContainsKey(ability))
            .ShouldBeEmpty("every ability needs its label in RulesReferenceTests.AbilityLabels and on docs/game/rules.md");

        var expected = Ruleset.Units
            .Select(static unit => Row(
                Ruleset.UnitName(unit.Id),
                Ruleset.ClassName(unit.Class),
                unit.Strength?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—",
                unit.Ranged is { } ranged ? $"{ranged.Strength} (range {ranged.Range})" : "—",
                unit.Movement,
                unit.Sight,
                unit.ProductionCost,
                unit.BuildProduction == 0 ? "—" : $"{unit.BuildProduction}",
                unit.Abilities.IsEmpty ? "—" : string.Join(", ", unit.Abilities.Select(static ability => AbilityLabels[ability]))))
            .ToList();

        Table("Unit types").ShouldBe(expected, ignoreOrder: true, Because);
    }

    [Fact]
    public void CostAndUpkeepTable_MatchesTheRuleset()
    {
        var units = Ruleset.Units.Select(static unit => Row(Ruleset.UnitName(unit.Id), Yields(unit.ResourceCost), Yields(unit.Upkeep)));
        var buildings = Ruleset.Buildings.Select(static building => Row(Ruleset.BuildingName(building.Id), Yields(building.ResourceCost), Yields(building.Upkeep)));

        Table("Costs and upkeep").ShouldBe([.. units, .. buildings], ignoreOrder: true, Because);
    }

    [Fact]
    public void ImprovementTable_MatchesTheRuleset()
    {
        var expected = Ruleset.Improvements
            .Select(static improvement => Row(
                Ruleset.ImprovementName(improvement.Id),
                string.Join(", ", improvement.Terrains.Select(static terrain => Ruleset.TerrainName(terrain))),
                improvement.Cost,
                Yields(improvement.Yields)))
            .ToList();

        Table("Improvements").ShouldBe(expected, ignoreOrder: true, Because);
    }

    [Fact]
    public void BuildingTable_MatchesTheRuleset()
    {
        var expected = Ruleset.Buildings
            .Select(static building => Row(
                Ruleset.BuildingName(building.Id),
                building.Cost,
                Yields(building.Yields),
                building.Production == 0 ? "—" : $"+{building.Production}",
                building.RequiresAdjacentTerrain.IsEmpty
                    ? "—"
                    : string.Join(" or ", building.RequiresAdjacentTerrain.Select(static terrain => Ruleset.TerrainName(terrain)))))
            .ToList();

        Table("City buildings").ShouldBe(expected, ignoreOrder: true, Because);
    }

    [Fact]
    public void CountersTable_MatchesTheRuleset()
    {
        var expected = Ruleset.Combat.Counters
            .Select(static counter => Row(Ruleset.ClassName(counter.Class), Ruleset.ClassName(counter.Against), $"+{counter.Bonus}"))
            .ToList();

        Table("Class counters").ShouldBe(expected, ignoreOrder: true, Because);
    }

    [Fact]
    public void GameNumbersTable_MatchesTheRuleset()
    {
        List<string> expected =
        [
            Row("Players", Ruleset.Setup.Players),
            Row("Map width", Ruleset.Map.DefaultWidth),
            Row("Map height", Ruleset.Map.DefaultHeight),
            Row("Starting units", string.Join(", ", Ruleset.Setup.StartingUnits.Select(static unit => Ruleset.UnitName(unit)))),
            Row("City sight", Ruleset.Cities.Sight),
            Row("Minimum distance between cities", Ruleset.Cities.MinimumSpacing),
            Row("Production per turn", Ruleset.Cities.ProductionPerTurn),
            Row("Territory of a new city (radius)", Ruleset.Territory.InitialRadius),
            Row("Largest territory (radius)", Ruleset.Territory.MaxRadius),
            Row("Price of the first new tile", $"{Ruleset.Territory.TileCost} {Ruleset.ResourceName(Ruleset.Territory.GrowthResource).ToLowerInvariant()}"),
            Row("Price increase per tile gained", Ruleset.Territory.TileCostIncrease),
            Row("Bridge cost", Ruleset.Bridges.Cost),
            Row("First unit a new city produces", Ruleset.UnitName(Ruleset.Cities.InitialProduction)),
            Row("Capturing a city resets its production", YesNo(Ruleset.Cities.CaptureResetsProduction)),
            Row("Full health", Ruleset.Combat.MaxHealth),
            Row("Damage between equal units", Ruleset.Combat.Damage.Base),
            Row("Extra damage per point of strength difference", Ruleset.Combat.Damage.PerStrengthPoint),
            Row("Least damage", Ruleset.Combat.Damage.Min),
            Row("Most damage", Ruleset.Combat.Damage.Max),
            Row("Random spread of damage", $"±{Ruleset.Combat.Damage.SpreadPercent}%"),
            Row("Healing per turn of rest", Ruleset.Combat.Healing.PerTurn),
            Row("Healing per turn of rest in your city", Ruleset.Combat.Healing.InOwnCity),
            Row("Land that supplies a unit outside your territory (radius)", Ruleset.Upkeep.SupplyRadius),
            Row("Health lost in a turn without upkeep", Ruleset.Upkeep.ShortageDamage),
            Row("Full health of a building", Ruleset.Upkeep.BuildingHealth),
            Row("Health a building regains per paid turn", Ruleset.Upkeep.BuildingRecovery),
        ];

        Table("Game numbers").ShouldBe(expected, ignoreOrder: true, Because);
    }

    private static string Row(params object[] cells) => string.Join(" | ", cells);

    private static string Yields(IEnumerable<ResourceAmount> yields) =>
        yields.Any() ? string.Join(", ", yields.Select(static amount => $"{amount.Amount} {Ruleset.ResourceName(amount.Resource)}")) : "—";

    private static string YesNo(bool value) => value ? "Yes" : "No";

    // The body rows of the first Markdown table after the "## <heading>" line, each as its trimmed cells joined by " | ".
    private static List<string> Table(string heading)
    {
        var lines = File.ReadAllLines(RulesFile);
        var start = Array.IndexOf(lines, $"## {heading}");
        start.ShouldBeGreaterThanOrEqualTo(0, $"docs/game/rules.md needs a '## {heading}' section");

        var rows = lines
            .Skip(start + 1)
            .TakeWhile(static line => !line.StartsWith("## ", StringComparison.Ordinal))
            .SkipWhile(static line => !line.StartsWith('|'))
            .TakeWhile(static line => line.StartsWith('|'))
            .Skip(2) // the header row and the separator
            .Select(static line => string.Join(" | ", line.Trim().Trim('|').Split('|').Select(static cell => cell.Trim())))
            .ToList();

        rows.ShouldNotBeEmpty($"the '## {heading}' section of docs/game/rules.md needs its table");
        return rows;
    }
}
