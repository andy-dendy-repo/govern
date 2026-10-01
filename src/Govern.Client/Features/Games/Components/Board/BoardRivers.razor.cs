using Govern.Client.Features.Games.Services;
using Govern.Core.Map;
using Govern.Core.Visibility;
using Microsoft.AspNetCore.Components;

namespace Govern.Client.Features.Games.Components.Board;

/// <summary>The known rivers along tile edges, and the bridges in sight over them.</summary>
public partial class BoardRivers
{
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

    // Each known river edge with the visibility of the tile that holds it.
    private IEnumerable<(HexEdge Edge, RiverType River, TileVisibility Visibility)> Rivers()
    {
        for (var index = 0; index < View.Tiles.Length; index++)
        {
            if (View.Tiles[index] is not { Rivers: { } rivers } tile)
            {
                continue;
            }

            foreach (var side in Enum.GetValues<HexSide>())
            {
                if (rivers.On(side) is { } river)
                {
                    yield return (new HexEdge(View.CoordAt(index), side), river, tile.Visibility);
                }
            }
        }
    }

    // Each bridged edge in sight.
    private IEnumerable<HexEdge> Bridges()
    {
        for (var index = 0; index < View.Tiles.Length; index++)
        {
            if (View.Tiles[index] is not { Bridges: { } bridges })
            {
                continue;
            }

            foreach (var side in Enum.GetValues<HexSide>().Where(bridges.On))
            {
                yield return new HexEdge(View.CoordAt(index), side);
            }
        }
    }
}
