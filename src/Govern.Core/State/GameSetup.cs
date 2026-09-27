using System.Collections.Immutable;
using Govern.Core.Map;

namespace Govern.Core.State;

/// <summary>
/// Everything needed to start a game with <see cref="GameFactory.Create"/>. The host validates user input before building it;
/// an invalid setup is a programming error and throws.
/// </summary>
/// <param name="Players">Exactly two distinct players, in turn order.</param>
/// <param name="Seed">Seed for spawn positions (and any later randomness).</param>
/// <param name="Width">Map columns, <see cref="GameMap.MinSize"/> to <see cref="GameMap.MaxSize"/>.</param>
/// <param name="Height">Map rows, <see cref="GameMap.MinSize"/> to <see cref="GameMap.MaxSize"/>.</param>
public sealed record GameSetup(ImmutableArray<PlayerId> Players, ulong Seed, int Width = GameSetup.DefaultWidth, int Height = GameSetup.DefaultHeight)
{
    /// <summary>Default map width.</summary>
    public const int DefaultWidth = 20;

    /// <summary>Default map height.</summary>
    public const int DefaultHeight = 14;

    /// <summary>Number of players a game supports.</summary>
    public const int PlayerCount = 2;
}
