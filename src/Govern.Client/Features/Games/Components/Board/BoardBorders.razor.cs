using Govern.Client.Features.Games.Helpers;
using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components.Board;

/// <summary>The borders of the territories in sight, in each owner's seat colour.</summary>
public partial class BoardBorders
{
    // How far towards the owned tile's centre a border line is moved, as a share of the way.
    private const double InsetShare = 0.12;

    private PlayerView? _rendered;

    /// <summary>The player's view.</summary>
    [Parameter, EditorRequired]
    public PlayerView View { get; set; } = null!;

    /// <inheritdoc/>
    protected override bool ShouldRender() => !ReferenceEquals(View, _rendered);

    /// <inheritdoc/>
    protected override void OnAfterRender(bool firstRender) => _rendered = View;

    // Every edge of an owned tile whose neighbour has another owner, no owner, or is off the map.
    private IEnumerable<(HexCoord Coord, HexCoord Neighbour, PlayerId Owner)> Borders()
    {
        for (var index = 0; index < View.Tiles.Length; index++)
        {
            if (View.Tiles[index].TerritoryOwner is not { } owner)
            {
                continue;
            }

            var coord = View.CoordAt(index);
            foreach (var neighbour in coord.Neighbors())
            {
                if (!View.Contains(neighbour) || View.TileAt(neighbour).TerritoryOwner != owner)
                {
                    yield return (coord, neighbour, owner);
                }
            }
        }
    }

    private static (double X1, double Y1, double X2, double Y2) Inset(HexCoord coord, HexCoord neighbour)
    {
        var (x1, y1, x2, y2) = HexLayout.EdgeEnds(HexEdge.Between(coord, neighbour)!.Value);
        var (cx, cy) = HexLayout.Center(coord);
        return (
            x1 + ((cx - x1) * InsetShare),
            y1 + ((cy - y1) * InsetShare),
            x2 + ((cx - x2) * InsetShare),
            y2 + ((cy - y2) * InsetShare));
    }
}
