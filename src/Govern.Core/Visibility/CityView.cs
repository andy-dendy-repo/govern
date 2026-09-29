using Govern.Core.Cities;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Visibility;

/// <summary>
/// A city as a player sees it. Production is shown only for the viewer's own cities: an enemy's progress would reveal when
/// their next unit appears.
/// </summary>
/// <param name="Id">City identifier.</param>
/// <param name="Owner">Player who controls the city.</param>
/// <param name="Name">Display name.</param>
/// <param name="Position">Tile the city stands on.</param>
/// <param name="Producing">Unit type the city is building; <see langword="null"/> for an enemy city.</param>
/// <param name="ProductionProgress">Production points towards <paramref name="Producing"/>; <see langword="null"/> for an enemy city.</param>
public sealed record CityView(CityId Id, PlayerId Owner, string Name, HexCoord Position, UnitType? Producing, int? ProductionProgress)
{
    /// <summary>Projects a city for a viewer, hiding the production of cities the viewer doesn't own.</summary>
    /// <param name="city">The city.</param>
    /// <param name="viewer">The player the view is for.</param>
    /// <returns>The city as the viewer sees it.</returns>
    public static CityView From(City city, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(city);

        return city.Owner == viewer
            ? new CityView(city.Id, city.Owner, city.Name, city.Position, city.Producing, city.ProductionProgress)
            : new CityView(city.Id, city.Owner, city.Name, city.Position, Producing: null, ProductionProgress: null);
    }
}
