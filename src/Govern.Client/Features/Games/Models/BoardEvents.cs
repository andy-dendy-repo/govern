using Govern.Core.Map;

namespace Govern.Client.Features.Games.Models;

/// <summary>What the board's renderer reports back to .NET (<see cref="Services.Board3DInteropService.MountAsync"/>).</summary>
/// <param name="TileClicked">Called with the tile the player clicked (a drag is never a click).</param>
/// <param name="TileHovered">
/// Called with the tile under the mouse or pen when it changes, and with <see langword="null"/> when the pointer leaves the map
/// or a drag starts; touch never hovers. <see langword="null"/> to ignore hovering.
/// </param>
/// <param name="ContextChanged">
/// Called with <see langword="true"/> when the browser takes the board's WebGL context away (the board stops drawing), and with
/// <see langword="false"/> once it gives it back and the board is drawn again. <see langword="null"/> to ignore it.
/// </param>
public sealed record BoardEvents(
    Func<HexCoord, Task> TileClicked,
    Func<HexCoord?, Task>? TileHovered = null,
    Func<bool, Task>? ContextChanged = null);
