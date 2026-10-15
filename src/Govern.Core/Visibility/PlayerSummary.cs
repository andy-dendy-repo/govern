using System.Text.Json.Serialization;
using Govern.Core.State;

namespace Govern.Core.Visibility;

/// <summary>
/// Public information about a participant (without what they have explored).
/// </summary>
/// <param name="Id">Player identifier.</param>
/// <param name="Status">Whether the player still takes part.</param>
/// <param name="EndedTurn">
/// In a simultaneous game, whether the player has ended this turn (every player sees it); left out of the JSON while false.
/// </param>
public sealed record PlayerSummary(
    PlayerId Id,
    PlayerStatus Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool EndedTurn = false);
