using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// The result of evaluating a path node by a profile.
/// </summary>
/// <param name="Modifier">
/// The cost modifier of the node: 1 is a normal tile. The cost of passing through the node is the modifier
/// multiplied by the distance.
/// </param>
/// <param name="Goal">The goal that the agent must perform at this node.</param>
/// <param name="GoalTargetKey">The key to which the coordinates of the node will be written for the goal.</param>
public readonly record struct PathNodeEval(
    float Modifier,
    ProtoId<UtilityAiGoalPrototype>? Goal,
    StateKey<EntityCoordinates> GoalTargetKey);
