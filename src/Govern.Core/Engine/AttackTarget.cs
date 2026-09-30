using Govern.Core.State;

namespace Govern.Core.Engine;

// What AttackRule.Check needs to know about the target tile; the rule builds it from the state, the preview from the view.
// Owner is the defending unit's, else the city's; HasUnit is whether a unit stands there.
internal sealed record AttackTarget(bool OnMap, PlayerId? Owner, bool HasUnit);
