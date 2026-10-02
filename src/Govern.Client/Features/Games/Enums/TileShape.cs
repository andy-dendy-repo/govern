namespace Govern.Client.Features.Games.Enums;

/// <summary>
/// What the 3D board builds on a tile besides its raised hex (<see cref="Helpers.TerrainReliefs"/>). The renderer
/// (<c>wwwroot/js/board3d/terrain.js</c>) has one builder per shape, named after it in lower case.
/// </summary>
public enum TileShape
{
    /// <summary>A plain tile top.</summary>
    Flat,

    /// <summary>A few low-poly trees.</summary>
    Trees,

    /// <summary>Rounded humps on a raised tile.</summary>
    Hills,

    /// <summary>Rocky peaks with snow caps.</summary>
    Peaks,

    /// <summary>A sunken tile under translucent water.</summary>
    Water,

    /// <summary>Low sand dunes.</summary>
    Dunes,
}
