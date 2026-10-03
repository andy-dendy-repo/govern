using Govern.Core.Cities;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Units;

namespace Govern.Client.Features.Games.Models;

/// <summary>What the player has selected on the board: one unit, one city, or nothing.</summary>
/// <param name="Unit">The selected unit (own or enemy), or <see langword="null"/>.</param>
/// <param name="City">The selected city (own or enemy), or <see langword="null"/>.</param>
/// <param name="Target">
/// A tile with an enemy unit the selected own unit is about to attack: its combat forecast is shown until the player attacks or
/// cancels. <see langword="null"/> when no attack is pending.
/// </param>
/// <param name="Bridge">
/// Whether the selected own unit is choosing where to build a bridge: the board marks the river edges it can bridge, and a
/// click on one starts the bridge.
/// </param>
/// <param name="Destination">
/// Whether the selected own unit is choosing a destination to walk to on its own: a click on any explored tile with a known way
/// there gives the order.
/// </param>
/// <param name="Placing">
/// The building the selected own city is choosing a tile for: the board marks the tiles of its territory that can take it, and
/// a click on one orders the building there. <see langword="null"/> when no tile is being chosen.
/// </param>
public sealed record BoardSelection(
    UnitId? Unit,
    CityId? City,
    HexCoord? Target = null,
    bool Bridge = false,
    BuildingType? Placing = null,
    bool Destination = false)
{
    /// <summary>Nothing selected.</summary>
    public static BoardSelection None { get; } = new(null, null);

    /// <summary>Selects a unit.</summary>
    /// <param name="unit">The unit.</param>
    /// <returns>The selection.</returns>
    public static BoardSelection Of(UnitId unit) => new(unit, null);

    /// <summary>Selects a city.</summary>
    /// <param name="city">The city.</param>
    /// <returns>The selection.</returns>
    public static BoardSelection Of(CityId city) => new(null, city);
}
