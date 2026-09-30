using Govern.Client.Features.Games.Enums;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>The <c>data-layer</c> names of the board's <see cref="BoardLayer"/> groups.</summary>
public static class BoardLayers
{
    /// <summary>The name a layer's <c>&lt;g data-layer="…"&gt;</c> carries (also its CSS class after <c>layer-</c>).</summary>
    /// <param name="layer">The layer.</param>
    /// <returns>The lowercase layer name.</returns>
    public static string Name(BoardLayer layer) => layer switch
    {
        BoardLayer.Terrain => "terrain",
        BoardLayer.Rivers => "rivers",
        BoardLayer.Improvements => "improvements",
        BoardLayer.Borders => "borders",
        BoardLayer.Lens => "lens",
        BoardLayer.Highlights => "highlights",
        BoardLayer.Cities => "cities",
        BoardLayer.Units => "units",
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown board layer."),
    };
}
