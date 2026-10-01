using Govern.Client.Features.Games.Helpers;
using Govern.Client.Features.Games.Services;
using Govern.Client.Helpers;
using Govern.Core.Economy;
using Govern.Core.Map;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components.Board;

/// <summary>The improvements and buildings in sight, and the buildings the player's cities are producing on their tiles.</summary>
public partial class BoardImprovements
{
    private const string SymbolScale = "0.6";
    private static readonly double Offset = HexLayout.Size * 0.45;
    private PlayerView? _rendered;

    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    [Inject]
    private RulesetService Rulesets { get; set; } = null!;

    /// <inheritdoc />
    protected override bool ShouldRender() => !ReferenceEquals(View, _rendered);

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender) => _rendered = View;

    // Places a symbol small in the tile's upper left.
    private static string Corner(HexCoord coord)
    {
        var (x, y) = HexLayout.Center(coord);
        return $"translate({HexLayout.Format(x - Offset)} {HexLayout.Format(y - Offset)}) scale({SymbolScale})";
    }

    private IEnumerable<(HexCoord Coord, ImprovementType Improvement)> Improvements()
    {
        for (var index = 0; index < View.Tiles.Length; index++)
        {
            if (View.Tiles[index].Improvement is { } improvement)
            {
                yield return (View.CoordAt(index), improvement);
            }
        }
    }

    // The finished buildings in sight, then the ones the player's cities are producing, on their reserved tiles, each with its
    // owner's seat class (a building stands in its city's territory, which a visible tile shows).
    private IEnumerable<(HexCoord Coord, BuildingType Building, string Seat, bool Planned)> Buildings()
    {
        for (var index = 0; index < View.Tiles.Length; index++)
        {
            if (View.Tiles[index] is { Building: { } building, TerritoryOwner: { } owner })
            {
                yield return (View.CoordAt(index), building, Seats.Class(View, owner), false);
            }
        }

        foreach (var city in View.Cities)
        {
            if (city is { ProducingBuilding: { } building, ProducingBuildingSite: { } site })
            {
                yield return (site, building, Seats.Class(View, city.Owner), true);
            }
        }
    }
}
