using Govern.Core.Cities;
using Govern.Core.Units;

namespace Govern.Client.Features.Games.Models;

/// <summary>What the player has selected on the board: one unit, one city, or nothing.</summary>
/// <param name="Unit">The selected unit (own or enemy), or <see langword="null"/>.</param>
/// <param name="City">The selected city (own or enemy), or <see langword="null"/>.</param>
public sealed record BoardSelection(UnitId? Unit, CityId? City)
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
