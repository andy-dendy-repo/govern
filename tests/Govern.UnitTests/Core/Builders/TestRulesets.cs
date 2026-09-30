using System.Text.Json.Nodes;
using Govern.Core.Map;
using Govern.Core.Rulesets;
using Govern.Core.Units;

namespace Govern.UnitTests.Core.Builders;

/// <summary>
/// The real default ruleset (<c>rulesets/default/v2.json</c>, copied next to the tests) and variants of it for rules whose data
/// matters to a test.
/// </summary>
internal static class TestRulesets
{
    public static readonly string DefaultJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "rulesets", "default", "v2.json"));

    public static readonly Ruleset Default = Ruleset.ParseValid(DefaultJson);

    public static readonly UnitType Settler = new("settler");
    public static readonly UnitType Soldier = new("soldier");
    public static readonly TerrainType Plains = new("plains");
    public static readonly TerrainType Forest = new("forest");
    public static readonly TerrainType Hills = new("hills");
    public static readonly TerrainType Mountains = new("mountains");
    public static readonly TerrainType Lake = new("lake");

    // The default document after a change, e.g. With(json => json["cities"]!["productionPerTurn"] = 2).
    public static JsonNode Document(Action<JsonNode>? change = null)
    {
        var document = JsonNode.Parse(DefaultJson)!;
        change?.Invoke(document);
        return document;
    }

    public static Ruleset With(Action<JsonNode> change) => Ruleset.ParseValid(Document(change).ToJsonString());

    // Adds a unit, with its name text, to a document.
    public static void AddUnit(JsonNode document, string id, int movement, int sight, int cost, params string[] abilities)
    {
        document["units"]!.AsArray().Add(new JsonObject
        {
            ["id"] = id,
            ["movement"] = movement,
            ["sight"] = sight,
            ["productionCost"] = cost,
            ["abilities"] = new JsonArray([.. abilities.Select(static ability => (JsonNode)ability)]),
        });
        document["texts"]![$"unit.{id}.name"] = id;
    }
}
