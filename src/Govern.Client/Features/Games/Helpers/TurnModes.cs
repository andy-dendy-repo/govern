using Govern.Core.State;

namespace Govern.Client.Features.Games.Helpers;

/// <summary>The words for a game's turn mode, in the lobby and the create-game picker.</summary>
public static class TurnModes
{
    /// <summary>The modes a new game can use, the default first.</summary>
    public static IReadOnlyList<TurnMode> All { get; } = [TurnMode.Sequential, TurnMode.Simultaneous];

    /// <summary>Names a turn mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>"One at a time" or "Everyone at once".</returns>
    public static string Describe(TurnMode mode) => mode == TurnMode.Simultaneous ? "Everyone at once" : "One at a time";
}
