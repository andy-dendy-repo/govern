using Govern.Core.Map;
using Govern.Core.State;
using Govern.Core.Units;

namespace Govern.Core.Engine;

// What AttackRule.Check needs to know about the target tile; the rule builds it from the state, the preview from the view.
// Owner is the defending unit's, else the city's; HasUnit is whether a unit stands there; River is the river on the edge
// between the attacker and the target (null when there is none or they aren't neighbours); Domain is the defending unit's
// domain (a city stands on land).
internal sealed record AttackTarget(bool OnMap, PlayerId? Owner, bool HasUnit, RiverType? River, UnitDomain Domain = UnitDomain.Land);
