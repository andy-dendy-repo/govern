using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Units;

namespace Govern.Client.Helpers;

/// <summary>
/// The symbol ids of the game's icon set (<c>Components/Game/GameSprite.razor</c>). Units and terrains are named after their
/// ruleset id (<c>unit-settler</c>, <c>terrain-plains</c>, <c>improvement-farm</c>, <c>building-campus</c>, <c>resource-food</c>), so a new ruleset entry needs only its symbol in the sprite.
/// </summary>
public static class Icons
{
    /// <summary>A city.</summary>
    public const string City = "city";

    /// <summary>Movement points.</summary>
    public const string Movement = "ui-movement";

    /// <summary>Sight range.</summary>
    public const string Sight = "ui-sight";

    /// <summary>Production.</summary>
    public const string Production = "ui-production";

    /// <summary>Health.</summary>
    public const string Health = "ui-health";

    /// <summary>Combat strength.</summary>
    public const string Strength = "ui-strength";

    /// <summary>Turns (time left).</summary>
    public const string Turns = "ui-turns";

    /// <summary>A bridge over a river.</summary>
    public const string Bridge = "ui-bridge";

    /// <summary>The symbol of a unit type.</summary>
    /// <param name="type">The unit type.</param>
    /// <returns><c>unit-</c> followed by the ruleset id.</returns>
    public static string Unit(UnitType type) => $"unit-{type.Id}";

    /// <summary>The symbol of a terrain type.</summary>
    /// <param name="type">The terrain type.</param>
    /// <returns><c>terrain-</c> followed by the ruleset id.</returns>
    public static string Terrain(TerrainType type) => $"terrain-{type.Id}";

    /// <summary>The symbol of a tile improvement.</summary>
    /// <param name="type">The improvement type.</param>
    /// <returns><c>improvement-</c> followed by the ruleset id.</returns>
    public static string Improvement(ImprovementType type) => $"improvement-{type.Id}";

    /// <summary>The symbol of a city building.</summary>
    /// <param name="type">The building type.</param>
    /// <returns><c>building-</c> followed by the ruleset id.</returns>
    public static string Building(BuildingType type) => $"building-{type.Id}";

    /// <summary>The symbol of a resource.</summary>
    /// <param name="type">The resource type.</param>
    /// <returns><c>resource-</c> followed by the ruleset id.</returns>
    public static string Resource(ResourceType type) => $"resource-{type.Id}";
}
