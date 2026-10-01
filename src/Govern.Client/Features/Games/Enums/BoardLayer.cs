namespace Govern.Client.Features.Games.Enums;

/// <summary>
/// The board's SVG layers, bottom to top. Each layer is a <c>&lt;g data-layer="…"&gt;</c> and renders in this order, so a
/// new kind of map content (rivers, buildings, borders) takes its slot instead of being stacked wherever it was added.
/// Layers with nothing to draw yet are not rendered. Lenses and highlights sit under the pieces so units stay readable.
/// </summary>
public enum BoardLayer
{
    /// <summary>The clickable tiles, coloured by terrain and visibility.</summary>
    Terrain,

    /// <summary>Rivers along tile edges (G6).</summary>
    Rivers,

    /// <summary>Tile improvements and buildings (G10).</summary>
    Improvements,

    /// <summary>Territory borders (G7).</summary>
    Borders,

    /// <summary>The active map lens.</summary>
    Lens,

    /// <summary>The selected unit's reachable and attackable tiles.</summary>
    Highlights,

    /// <summary>Cities and their names.</summary>
    Cities,

    /// <summary>Units.</summary>
    Units,

    /// <summary>Short-lived animation effects: damage numbers and pulses on tiles (F11b).</summary>
    Effects,
}
