using Govern.Client.Features.Games.Models;
using Govern.Core.Visibility;

namespace Govern.Client.Features.Games.Lenses;

/// <summary>
/// A map lens: a toggleable overlay that marks every tile with one kind of information (movement cost, later resources or
/// territory). Lenses are registered in <c>DependencyInjection</c>; <c>LensToolbar</c> lists them and <c>BoardLens</c> draws
/// the active one in the <c>BoardLayer.Lens</c> slot. Adding a lens is one class and one registration.
/// </summary>
public interface IMapLens
{
    /// <summary>A stable id, used in CSS classes and test ids (<c>movement-cost</c>).</summary>
    string Id { get; }

    /// <summary>The name on the toolbar button.</summary>
    string Name { get; }

    /// <summary>The icon of the toolbar button (see <c>Helpers/Icons</c>).</summary>
    string Icon { get; }

    /// <summary>What the lens shows on a tile.</summary>
    /// <param name="tile">The tile as the player sees it.</param>
    /// <returns>The mark, or <see langword="null"/> to leave the tile alone (always for an unexplored tile).</returns>
    LensMark? MarkFor(TileView tile);

    /// <summary>What the lens shows on the edges a tile owns (<see cref="Core.Map.HexSide"/>), such as river crossings.</summary>
    /// <param name="tile">The tile as the player sees it.</param>
    /// <returns>The marks; none by default.</returns>
    IEnumerable<EdgeMark> EdgeMarksFor(TileView tile) => [];
}
