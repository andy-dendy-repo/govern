using System.Text.Json.Serialization;

namespace Govern.Core.Units;

/// <summary>Where a unit moves: on land or on water.</summary>
public enum UnitDomain
{
    /// <summary>Walks passable land tiles.</summary>
    [JsonStringEnumMemberName("land")]
    Land,

    /// <summary>Sails water tiles.</summary>
    [JsonStringEnumMemberName("naval")]
    Naval,
}
