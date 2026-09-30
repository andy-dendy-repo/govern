using Govern.Core.Cities;
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
public sealed record BoardSelection(UnitId? Unit, CityId? City, HexCoord? Target = null)
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
