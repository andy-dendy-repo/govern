namespace Govern.Client.Features.Design.Models;

/// <summary>What the 3D board bench (<c>/design/board</c>) measured on the board it shows; times in milliseconds.</summary>
/// <param name="SceneBuild">Building the <c>BoardScene</c> from the view in C# (<c>BoardSceneBuilder</c>), averaged.</param>
/// <param name="Apply">Applying the last scene sent in the renderer (only the layers that changed are built).</param>
/// <param name="Layers">The time each layer took when it was last built, by layer name, and <c>keys</c>: comparing the layers' inputs.</param>
/// <param name="Rebuild">Building every layer again, as a theme change does.</param>
/// <param name="Frame">The time between two frames while panning (the browser's own work included), averaged.</param>
/// <param name="FrameP95">The time between two frames while panning, 95th percentile.</param>
/// <param name="Labels">Placing the HTML labels in one panning frame, averaged.</param>
/// <param name="Draw">Drawing the scene in one panning frame (the work handed to the GPU), averaged.</param>
/// <param name="LabelCount">The HTML labels over the board.</param>
/// <param name="Calls">Draw calls in one frame.</param>
/// <param name="Triangles">Triangles drawn in one frame.</param>
/// <param name="PixelRatio">The renderer's pixel ratio.</param>
/// <param name="ShadowMap">The sun's shadow map size in pixels.</param>
/// <param name="Shares">Triangles each layer draws per pass, by layer name.</param>
public sealed record BoardBenchResult(
    double SceneBuild,
    double Apply,
    IReadOnlyDictionary<string, double> Layers,
    double Rebuild,
    double Frame,
    double FrameP95,
    double Labels,
    double Draw,
    int LabelCount,
    int Calls,
    int Triangles,
    double PixelRatio,
    int ShadowMap,
    IReadOnlyDictionary<string, int> Shares);
