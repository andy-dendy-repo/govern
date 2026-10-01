using Govern.Application.Games.Services;
using Govern.ArchitectureTests.Support;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.ArchitectureTests;

/// <summary>
/// The player-facing rules reference (<c>docs/game/rules.md</c>) documents the ruleset the game is played with: every terrain
/// and unit has one row with the ruleset's numbers, every class counter has one row, and the "Game numbers" table matches the
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
                terrain.DefenceBonus == 0 ? "—" : $"+{terrain.DefenceBonus}"))
            .ToList();

        Table("Terrain types").ShouldBe(expected, ignoreOrder: true, Because);
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
                string.Join(", ", unit.Abilities.Select(static ability => AbilityLabels[ability]))))
            .ToList();

        Table("Unit types").ShouldBe(expected, ignoreOrder: true, Because);
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
        ];

        Table("Game numbers").ShouldBe(expected, ignoreOrder: true, Because);
    }

    private static string Row(params object[] cells) => string.Join(" | ", cells);

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
