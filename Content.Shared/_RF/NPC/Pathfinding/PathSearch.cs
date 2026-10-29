using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
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
/// A goal that the agent must perform when it reaches a node of the path.
/// </summary>
/// <param name="Node">The node of the path.</param>
/// <param name="Goal">The goal that must be performed.</param>
/// <param name="TargetKey">The key to which the coordinates of the node will be written for the goal.</param>
/// <param name="RangeKey">The key of the agent's state that holds how far from the node it stops for the goal.</param>
public readonly record struct PathNodeGoal(
    PathNodeRef Node,
    ProtoId<UtilityAiGoalPrototype> Goal,
    StateKey<EntityCoordinates> TargetKey,
    StateKey<float> RangeKey);

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
public readonly record struct PathSearch(
    Entity<PathProfilesComponent, GoapComponent> Agent,
    EntityCoordinates Start,
    EntityCoordinates? Target,
    float Range,
    PathJob Job);

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
