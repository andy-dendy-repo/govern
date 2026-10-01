using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Units;

namespace Govern.Core.Rulesets;

/// <summary>
/// Every customisable number, list and text of a game, read from a ruleset JSON document (see <c>docs/game/game-data.md</c>).
/// Core holds the mechanics; the ruleset holds the data they use. A game always runs with the one ruleset it was started with.
/// Immutable, so one instance can be shared by every game and request.
/// </summary>
public sealed class Ruleset
{
    private readonly FrozenDictionary<UnitType, UnitDefinition> _units;
    private readonly FrozenDictionary<TerrainType, TerrainDefinition> _terrains;
    private readonly FrozenDictionary<RiverType, RiverDefinition> _rivers;
    private readonly FrozenDictionary<ResourceType, ResourceDefinition> _resources;
    private readonly FrozenDictionary<ImprovementType, ImprovementDefinition> _improvements;
    private readonly FrozenDictionary<BuildingType, BuildingDefinition> _buildings;

    internal Ruleset(RulesetDocument document, string hash)
    {
        Setup = document.Setup;
        Map = document.Map;
        Terrains = document.Terrains;
        Rivers = document.Rivers;
        Bridges = document.Bridges;
        Units = document.Units;
        Cities = document.Cities;
        Resources = document.Resources;
        Improvements = document.Improvements;
        Buildings = document.Buildings;
        Combat = document.Combat;
        Texts = document.Texts;
        Hash = hash;
        _units = document.Units.ToFrozenDictionary(static unit => unit.Id);
        _terrains = document.Terrains.ToFrozenDictionary(static terrain => terrain.Id);
        _rivers = document.Rivers.ToFrozenDictionary(static river => river.Id);
        _resources = document.Resources.ToFrozenDictionary(static resource => resource.Id);
        _improvements = document.Improvements.ToFrozenDictionary(static improvement => improvement.Id);
        _buildings = document.Buildings.ToFrozenDictionary(static building => building.Id);
    }

    /// <summary>How a game starts.</summary>
    public SetupRules Setup { get; }

    /// <summary>The board.</summary>
    public MapRules Map { get; }

    /// <summary>Every terrain, in the document's order.</summary>
    public ImmutableArray<TerrainDefinition> Terrains { get; }

    /// <summary>Every river kind, in the document's order.</summary>
    public ImmutableArray<RiverDefinition> Rivers { get; }

    /// <summary>Bridges over rivers.</summary>
    public BridgeRules Bridges { get; }

    /// <summary>Every unit, in the document's order (the order the client lists them in).</summary>
    public ImmutableArray<UnitDefinition> Units { get; }

    /// <summary>Cities.</summary>
    public CityRules Cities { get; }

    /// <summary>Every resource, in the document's order (the order stockpiles and yields are listed in).</summary>
    public ImmutableArray<ResourceDefinition> Resources { get; }

    /// <summary>Every tile improvement, in the document's order.</summary>
    public ImmutableArray<ImprovementDefinition> Improvements { get; }

    /// <summary>Every city building, in the document's order.</summary>
    public ImmutableArray<BuildingDefinition> Buildings { get; }

    /// <summary>Health, damage, healing and counters.</summary>
    public CombatRules Combat { get; }

    /// <summary>Every text a player reads, by key (English only for now).</summary>
    public ImmutableDictionary<string, string> Texts { get; }

    /// <summary>The SHA-256 of the exact document text, as lowercase hex (<see cref="ComputeHash"/>).</summary>
    public string Hash { get; }

    /// <summary>Parses and validates a ruleset document.</summary>
    /// <param name="json">The document text, exactly as stored; its hash becomes <see cref="Hash"/>.</param>
    /// <returns>The ruleset, or every problem found.</returns>
    public static RulesetParseResult Parse(string json) => RulesetParser.Parse(json);

    /// <summary>Parses a ruleset document that is known to be valid, such as one already published.</summary>
    /// <param name="json">The document text.</param>
    /// <returns>The ruleset.</returns>
    /// <exception cref="FormatException">The document is not a valid ruleset; the message lists every problem.</exception>
    public static Ruleset ParseValid(string json)
    {
        var result = Parse(json);
        return result.IsValid
            ? result.Ruleset
            : throw new FormatException($"The ruleset is invalid: {string.Join("; ", result.Problems)}");
    }

    /// <summary>The hash of a ruleset document: SHA-256 of its UTF-8 bytes, as lowercase hex. Any change, even whitespace, changes it.</summary>
    /// <param name="json">The document text.</param>
    /// <returns>64 lowercase hex characters.</returns>
    public static string ComputeHash(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>Looks up a unit type that may not exist, such as one sent in a command.</summary>
    /// <param name="type">The unit type.</param>
    /// <returns>Its definition, or <see langword="null"/> when the ruleset has no such unit.</returns>
    public UnitDefinition? FindUnit(UnitType type) => _units.GetValueOrDefault(type);

    /// <summary>The definition of a unit type that is in the game.</summary>
    /// <param name="type">A unit type of this ruleset.</param>
    /// <returns>Its definition.</returns>
    /// <exception cref="ArgumentException">The ruleset has no such unit (a programming error).</exception>
    public UnitDefinition Unit(UnitType type) =>
        FindUnit(type) ?? throw new ArgumentException($"The ruleset has no unit '{type.Id}'.", nameof(type));

    /// <summary>The definition of a terrain that is in the game.</summary>
    /// <param name="type">A terrain of this ruleset.</param>
    /// <returns>Its definition.</returns>
    /// <exception cref="ArgumentException">The ruleset has no such terrain (a programming error).</exception>
    public TerrainDefinition Terrain(TerrainType type) =>
        _terrains.GetValueOrDefault(type) ?? throw new ArgumentException($"The ruleset has no terrain '{type.Id}'.", nameof(type));

    /// <summary>The definition of a river kind that is in the game.</summary>
    /// <param name="type">A river kind of this ruleset.</param>
    /// <returns>Its definition.</returns>
    /// <exception cref="ArgumentException">The ruleset has no such river (a programming error).</exception>
    public RiverDefinition River(RiverType type) =>
        _rivers.GetValueOrDefault(type) ?? throw new ArgumentException($"The ruleset has no river '{type.Id}'.", nameof(type));

    /// <summary>Looks up a tile improvement that may not exist, such as one sent in a command.</summary>
    /// <param name="type">The improvement.</param>
    /// <returns>Its definition, or <see langword="null"/> when the ruleset has no such improvement.</returns>
    public ImprovementDefinition? FindImprovement(ImprovementType type) => _improvements.GetValueOrDefault(type);

    /// <summary>The definition of a tile improvement that is in the game.</summary>
    /// <param name="type">An improvement of this ruleset.</param>
    /// <returns>Its definition.</returns>
    /// <exception cref="ArgumentException">The ruleset has no such improvement (a programming error).</exception>
    public ImprovementDefinition Improvement(ImprovementType type) =>
        FindImprovement(type) ?? throw new ArgumentException($"The ruleset has no improvement '{type.Id}'.", nameof(type));

    /// <summary>Looks up a city building that may not exist, such as one sent in a command.</summary>
    /// <param name="type">The building.</param>
    /// <returns>Its definition, or <see langword="null"/> when the ruleset has no such building.</returns>
    public BuildingDefinition? FindBuilding(BuildingType type) => _buildings.GetValueOrDefault(type);

    /// <summary>The definition of a city building that is in the game.</summary>
    /// <param name="type">A building of this ruleset.</param>
    /// <returns>Its definition.</returns>
    /// <exception cref="ArgumentException">The ruleset has no such building (a programming error).</exception>
    public BuildingDefinition Building(BuildingType type) =>
        FindBuilding(type) ?? throw new ArgumentException($"The ruleset has no building '{type.Id}'.", nameof(type));

    /// <summary>Looks up a resource that may not exist.</summary>
    /// <param name="type">The resource.</param>
    /// <returns>Its definition, or <see langword="null"/> when the ruleset has no such resource.</returns>
    public ResourceDefinition? FindResource(ResourceType type) => _resources.GetValueOrDefault(type);

    /// <summary>A text by key.</summary>
    /// <param name="key">A key the ruleset uses, such as <c>unit.settler.name</c>.</param>
    /// <returns>The English text.</returns>
    /// <exception cref="ArgumentException">The ruleset has no such text.</exception>
    public string Text(string key) =>
        Texts.GetValueOrDefault(key) ?? throw new ArgumentException($"The ruleset has no text '{key}'.", nameof(key));

    /// <summary>The display name of a unit type.</summary>
    /// <param name="type">A unit type of this ruleset.</param>
    /// <returns>The text <c>unit.{id}.name</c>.</returns>
    public string UnitName(UnitType type) => Text($"unit.{type.Id}.name");

    /// <summary>The display name of a terrain.</summary>
    /// <param name="type">A terrain of this ruleset.</param>
    /// <returns>The text <c>terrain.{id}.name</c>.</returns>
    public string TerrainName(TerrainType type) => Text($"terrain.{type.Id}.name");

    /// <summary>The display name of a river kind.</summary>
    /// <param name="type">A river kind of this ruleset.</param>
    /// <returns>The text <c>river.{id}.name</c>.</returns>
    public string RiverName(RiverType type) => Text($"river.{type.Id}.name");

    /// <summary>The display name of a resource.</summary>
    /// <param name="type">A resource of this ruleset.</param>
    /// <returns>The text <c>resource.{id}.name</c>.</returns>
    public string ResourceName(ResourceType type) => Text($"resource.{type.Id}.name");

    /// <summary>The display name of a tile improvement.</summary>
    /// <param name="type">An improvement of this ruleset.</param>
    /// <returns>The text <c>improvement.{id}.name</c>.</returns>
    public string ImprovementName(ImprovementType type) => Text($"improvement.{type.Id}.name");

    /// <summary>The display name of a city building.</summary>
    /// <param name="type">A building of this ruleset.</param>
    /// <returns>The text <c>building.{id}.name</c>.</returns>
    public string BuildingName(BuildingType type) => Text($"building.{type.Id}.name");

    /// <summary>The display name of a unit class.</summary>
    /// <param name="unitClass">A class used by a unit of this ruleset (<see cref="UnitDefinition.Class"/>).</param>
    /// <returns>The text <c>class.{id}.name</c>.</returns>
    public string ClassName(string unitClass) => Text($"class.{unitClass}.name");

    /// <summary>The name of a player's new city.</summary>
    /// <param name="number">The founder's city count including the new one.</param>
    /// <returns>The <see cref="CityRules.NameTemplate"/> text with <c>{n}</c> replaced.</returns>
    public string CityName(int number) =>
        Text(Cities.NameTemplate).Replace("{n}", number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
