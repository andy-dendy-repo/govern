using Govern.Core.Map;
using Govern.Core.State;

namespace Govern.Core.Units;

/// <summary>
/// What the rules need to know of a unit on a tile, shared by the state's <see cref="Unit"/> and the view's
/// <see cref="Visibility.UnitView"/>, so a rule and its view preview pick the same unit (for example the one an attack hits).
/// </summary>
public interface IUnitOnMap
{
    /// <summary>The unit's owner.</summary>
    PlayerId Owner { get; }

    /// <summary>The unit's type.</summary>
    UnitType Type { get; }

    /// <summary>The tile the unit stands on.</summary>
    HexCoord Position { get; }
}
