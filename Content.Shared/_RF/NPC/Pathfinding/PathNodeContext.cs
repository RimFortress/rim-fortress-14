using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared.Damage;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// The context in which the conditions of the path node are checked and its cost is calculated.
/// </summary>
/// <param name="Agent">The agent for which the path is being searched.</param>
/// <param name="Damage">How much damage per second the agent is able to deal.</param>
/// <param name="Facts">Facts about the node that is being evaluated.</param>
/// <param name="Blocked">
/// Is the node physically blocked for the agent (its collision overlaps the agent's collision mask).
/// </param>
/// <param name="Variables">Numeric values written by conditions, they can be used in the rule's formulas.</param>
public readonly record struct PathNodeContext(
    Entity<GoapComponent> Agent,
    DamageSpecifier Damage,
    PathNodeFacts Facts,
    bool Blocked,
    Dictionary<string, float> Variables);
