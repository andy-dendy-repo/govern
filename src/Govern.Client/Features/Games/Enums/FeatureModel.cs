namespace Govern.Client.Features.Games.Enums;

/// <summary>
/// The small model the 3D board builds for a tile improvement or building (<see cref="Helpers.FeatureModels"/>). The renderer
/// (<c>wwwroot/js/board3d/models.js</c>) has one builder per model, named after it in lower case, and never looks at a ruleset id.
/// </summary>
public enum FeatureModel
{
    /// <summary>A tilled field with furrows.</summary>
    Field,

    /// <summary>A log pile next to a small hut.</summary>
    Camp,

    /// <summary>A rock mound with a timber entrance.</summary>
    Mine,

    /// <summary>A few tents and a flag.</summary>
    Tents,

    /// <summary>A hall with columns under a pointed roof.</summary>
    Hall,

    /// <summary>A building under a dome.</summary>
    Dome,

    /// <summary>A market stall with crates.</summary>
    Stall,

    /// <summary>A pier with a boathouse.</summary>
    Pier,
}
