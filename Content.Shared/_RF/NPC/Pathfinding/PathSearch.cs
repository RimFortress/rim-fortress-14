using System.Numerics;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding;

/// <summary>
/// A reference to a node of the navigation map. The map can be rebuilt while a search is in progress,
/// so a node is stored by its identity and not by a reference to the object of the map.
/// </summary>
/// <param name="Graph">The grid that owns the node.</param>
/// <param name="Chunk">The origin of the chunk of the grid.</param>
/// <param name="Tile">The index of the tile in the chunk.</param>
/// <param name="Box">The region of the tile, there can be several nodes in a tile.</param>
public readonly record struct PathNodeRef(EntityUid Graph, Vector2i Chunk, byte Tile, Box2 Box);

/// <summary>
/// When the goal of a path node is performed.
/// </summary>
public enum PathGoalPhase : byte
{
    /// <summary>
    /// Before the agent goes through the node: it stops short of it, the goal is performed, and then the path is searched for
    /// again. It is how a door is opened.
    /// </summary>
    Before,

    /// <summary>
    /// After the agent has gone through the node and is far enough from it. It is how a door is closed behind.
    /// </summary>
    After,
}

/// <summary>
/// What the agent performs at a path node, as the rule that gave it describes.
/// </summary>
/// <param name="Goal">The goal that must be performed.</param>
/// <param name="TargetKey">The key to which the coordinates of the node will be written for the goal.</param>
/// <param name="RangeKey">
/// The key of the agent's state that holds how far from the node it stops for the goal, or how far past it the goal is
/// performed.
/// </param>
/// <param name="EntityTag">
/// The path tag of the entity at the node that the goal is for, such as a door. Null if the goal is not for an entity.
/// </param>
/// <param name="EntityKey">The key to which the entity at the node, the one with the tag, will be written.</param>
public readonly record struct PathGoalInfo(
    ProtoId<UtilityAiGoalPrototype> Goal,
    StateKey<EntityCoordinates> TargetKey,
    StateKey<float> RangeKey,
    ProtoId<PathTagPrototype>? EntityTag,
    StateKey<EntityUid>? EntityKey);

/// <summary>
/// A goal that the agent must perform at a node of the path.
/// </summary>
/// <param name="Node">The node of the path.</param>
/// <param name="Info">What is performed.</param>
/// <param name="Phase">When it is performed, in relation to the agent going through the node.</param>
public readonly record struct PathNodeGoal(PathNodeRef Node, PathGoalInfo Info, PathGoalPhase Phase);

/// <summary>
/// A place the path should not go by: it costs more the closer to the place a node is. It is how a request remembers
/// the places that did not work, where the agent got stuck, so that the next path does not lead there again.
/// </summary>
/// <param name="Graph">The grid.</param>
/// <param name="Position">The place, in the coordinates of the grid.</param>
/// <param name="Radius">How far from the place the nodes cost more.</param>
/// <param name="Cost">How much more a node at the place costs, it falls to nothing at the radius.</param>
public readonly record struct PathAvoid(EntityUid Graph, Vector2 Position, float Radius, float Cost);

/// <summary>
/// The input of a path search algorithm.
/// </summary>
/// <param name="Agent">
/// The agent that searches for the path. It must have started a request with
/// <see cref="Systems.SharedRfPathfindingSystem.BeginRequest"/>, the cost of nodes is calculated by its profile.
/// </param>
/// <param name="Start">Where the path starts.</param>
/// <param name="Target">Where the path must lead, null if the algorithm does not search for a destination.</param>
/// <param name="Range">How close to the <paramref name="Target"/> the agent needs to get.</param>
/// <param name="Job">The job that runs the search, the algorithm must yield through it.</param>
/// <param name="TargetBounds">
/// The region the target takes up, in the coordinates of the entity of <paramref name="Target"/>. If it is set,
/// the range is counted from the region and not from the point: a target that is big and solid cannot be reached
/// at its center.
/// </param>
/// <param name="Avoid">The places the path should not go by, null if there are none.</param>
public readonly record struct PathSearch(
    Entity<PathProfilesComponent, GoapComponent> Agent,
    EntityCoordinates Start,
    EntityCoordinates? Target,
    float Range,
    PathJob Job,
    Box2? TargetBounds = null,
    IReadOnlyList<PathAvoid>? Avoid = null);

public enum PathSearchStatus : byte
{
    /// <summary>
    /// The destination cannot be reached.
    /// </summary>
    NoPath,

    /// <summary>
    /// The path leads to the closest reachable point, not to the destination.
    /// </summary>
    PartialPath,

    /// <summary>
    /// The path leads to the destination.
    /// </summary>
    Path,
}

/// <summary>
/// The result of a path search.
/// </summary>
/// <param name="Status">Whether the path was found.</param>
/// <param name="Path">Nodes of the path from the start to the end.</param>
/// <param name="Goals">Goals that the agent must perform on the way, in the order of the path.</param>
public readonly record struct PathSearchResult(
    PathSearchStatus Status,
    List<PathNodeRef> Path,
    List<PathNodeGoal> Goals)
{
    public static PathSearchResult NoPath => new(PathSearchStatus.NoPath, new(), new());
}
