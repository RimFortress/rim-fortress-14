namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// The result of evaluating a path node by a profile.
/// </summary>
/// <param name="Modifier">
/// The cost modifier of the node: 1 is a normal tile. The cost of passing through the node is the modifier
/// multiplied by the distance.
/// </param>
/// <param name="Before">The goal that the agent must perform before it goes through this node.</param>
/// <param name="After">The goal that the agent must perform after it has gone through this node.</param>
public readonly record struct PathNodeEval(float Modifier, PathGoalInfo? Before, PathGoalInfo? After);
