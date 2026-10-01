namespace Govern.Client.Features.Games.Enums;

/// <summary>
/// The kinds of short animation effect drawn in the board's effects layer.
/// </summary>
public enum BoardEffectKind
{
    /// <summary>A damage number that floats up and fades (an attack).</summary>
    Damage,

    /// <summary>A ring that grows and fades on a tile (a city founded or captured, an improvement or bridge built).</summary>
    Pulse,
}
