namespace Govern.Client.Features.Games.Enums;

/// <summary>
/// The model the 3D board builds for a unit or a city (<see cref="Helpers.PieceModels"/>). The renderer
/// (<c>wwwroot/js/board3d/pieces.js</c>) has one builder per model, named after it in lower case, and never looks at a ruleset id.
/// </summary>
public enum PieceModel
{
    /// <summary>A covered wagon.</summary>
    Settler,

    /// <summary>A figure with a pick.</summary>
    Builder,

    /// <summary>A figure with a sword and a shield.</summary>
    Soldier,

    /// <summary>A figure with a long spear.</summary>
    Spearman,

    /// <summary>A rider on a horse.</summary>
    Horseman,

    /// <summary>A figure with a bow.</summary>
    Archer,

    /// <summary>A hooded figure with a knife.</summary>
    Scout,

    /// <summary>A cluster of houses round a tower.</summary>
    City,
}
