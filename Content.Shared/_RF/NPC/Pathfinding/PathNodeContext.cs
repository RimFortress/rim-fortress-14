using System.Numerics;
using Content.Shared._RF.Combat;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// The context in which the conditions of the path node are checked and its cost is calculated.
/// </summary>
/// <param name="Agent">The agent for which the path is being searched.</param>
/// <param name="Attacks">All the ways the agent is able to deal damage.</param>
/// <param name="Facts">Facts about the node that is being evaluated.</param>
/// <param name="Blocked">
/// Is the node physically blocked for the agent (its collision overlaps the agent's collision mask).
/// </param>
/// <param name="Variables">Numeric values written by conditions, they can be used in the rule's formulas.</param>
/// <param name="Point">Where the node is, in the coordinates of its grid. It is only known for the rules of dynamic tags.</param>
/// <param name="Graph">The grid of the node.</param>
/// <param name="Dynamic">Where the sources of the dynamic tags are, for the request.</param>
public readonly record struct PathNodeContext(
    Entity<GoapComponent> Agent,
    List<CombatAttack> Attacks,
    PathNodeFacts Facts,
    bool Blocked,
    Dictionary<string, float> Variables,
    ProtoId<PathQueryPrototype>? Query = null,
    Vector2 Point = default,
    EntityUid Graph = default,
    PathDynamicInfluence? Dynamic = null);
